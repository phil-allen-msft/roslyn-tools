// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the License.txt file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace GithubMergeTool
{
    public class GithubMergeTool
    {
        private static readonly Uri GithubBaseUri = new Uri("https://api.github.com/");

        private readonly IHttpClientDecorator _client;

        public GithubMergeTool(
            string username,
            string password,
            bool isDryRun)
        {
            var client = new HttpClient
            {
                BaseAddress = GithubBaseUri
            };

            var authArray = Encoding.ASCII.GetBytes($"{username}:{password}");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                "Basic",
                Convert.ToBase64String(authArray));
            client.DefaultRequestHeaders.Add(
                "user-agent",
                "Mozilla/4.0 (compatible; MSIE 6.0; Windows NT 5.2;)");

            // Needed to call the check-runs endpoint
            client.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/vnd.github.antiope-preview+json"));

            if (isDryRun)
            {
                _client = new NoOpHttpClientDecorator(client);
            }
            else
            {
                _client = new HttpClientDecorator(client);
            }
        }

        // Used for testing
        public GithubMergeTool(IHttpClientDecorator client)
        {
            _client = client;
        }

        private static readonly JsonSerializerOptions s_jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = null,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        // Concrete types to replace DeserializeAnonymousType patterns

        private class CompareResponse
        {
            [JsonPropertyName("status")]
            public string Status { get; set; }
            [JsonPropertyName("ahead_by")]
            public int AheadBy { get; set; }
            [JsonPropertyName("behind_by")]
            public int BehindBy { get; set; }
            [JsonPropertyName("total_commits")]
            public int TotalCommits { get; set; }
        }

        private class RefObject
        {
            [JsonPropertyName("sha")]
            public string Sha { get; set; }
        }

        private class SourceBranchResponse
        {
            [JsonPropertyName("object")]
            public RefObject Object { get; set; }
        }

        private class PrHead
        {
            [JsonPropertyName("sha")]
            public string Sha { get; set; }
        }

        private class ExistingPrItem
        {
            [JsonPropertyName("title")]
            public string Title { get; set; }
            [JsonPropertyName("number")]
            public string Number { get; set; }
            [JsonPropertyName("head")]
            public PrHead Head { get; set; }
        }

        private class CreatePrResponse
        {
            [JsonPropertyName("number")]
            public string Number { get; set; }
            [JsonPropertyName("node_id")]
            public string NodeId { get; set; }
            [JsonPropertyName("mergeable")]
            public bool? Mergeable { get; set; }
        }

        private class AssigneeItem
        {
            [JsonPropertyName("login")]
            public string Login { get; set; }
        }

        private class AssigneeResponse
        {
            [JsonPropertyName("assignees")]
            public AssigneeItem[] Assignees { get; set; }
        }

        private class AutoMergeError
        {
            [JsonPropertyName("message")]
            public string Message { get; set; }
        }

        private class AutoMergeResponse
        {
            [JsonPropertyName("errors")]
            public AutoMergeError[] Errors { get; set; }
        }

        private class PrStatusLabel
        {
            [JsonPropertyName("name")]
            public string Name { get; set; }
        }

        private class PrStatusResponse
        {
            [JsonPropertyName("mergeable")]
            public bool? Mergeable { get; set; }
            [JsonPropertyName("mergeable_state")]
            public string MergeableState { get; set; }
            [JsonPropertyName("labels")]
            public PrStatusLabel[] Labels { get; set; }
        }

        private class SearchItem
        {
            [JsonPropertyName("title")]
            public string Title { get; set; }
            [JsonPropertyName("number")]
            public int Number { get; set; }
        }

        private class SearchResponse
        {
            [JsonPropertyName("items")]
            public SearchItem[] Items { get; set; }
        }

        /// <summary>
        /// Create a merge PR.
        /// </summary>
        /// <returns>
        /// (true, null) if a PR was created without error
        /// (true, error) if a PR was created but an error occurred
        /// (false, null) if the PR was not created, for example if the branches are already in sync
        /// (false, error) if the PR was not created due to an error
        /// </returns>
        public async Task<(bool prCreated, HttpResponseMessage response)> CreateMergePr(
            string repoOwner,
            string repoName,
            List<string> prOwners,
            string srcBranch,
            string destBranch,
            bool updateExistingPr,
            bool addAutoMergeLabel,
            bool isAutoTriggered)
        {
            // Check to see how far ahead the source branch is
            // https://developer.github.com/v3/repos/commits/#compare-two-commits
            HttpResponseMessage compareResponse = await _client.GetAsync(
                $"repos/{repoOwner}/{repoName}/compare/{destBranch}...{srcBranch}");

            if (compareResponse.StatusCode != HttpStatusCode.OK)
            {
                return (false, compareResponse);
            }

            var compareData = JsonSerializer.Deserialize<CompareResponse>(
                await compareResponse.Content.ReadAsStringAsync(), s_jsonOptions);

            var branchesSynched = compareData.BehindBy == 0;
            if (branchesSynched)
            {
                Console.WriteLine("The branches are already synched.");
                return (false, null); // `null` means the branch is all caught up.
            }

            // Get the SHA for the source branch
            // https://developer.github.com/v3/git/refs/#get-a-single-reference
            var response = await _client.GetAsync($"repos/{repoOwner}/{repoName}/git/refs/heads/{srcBranch}");

            if (response.StatusCode != HttpStatusCode.OK)
            {
                return (false, response);
            }

            var sourceBranchData = JsonSerializer.Deserialize<SourceBranchResponse>(
                await response.Content.ReadAsStringAsync(), s_jsonOptions);

            var srcSha = sourceBranchData.Object.Sha;

            string prTitle = $"Merge {srcBranch} to {destBranch}";
            string prBranchName = $"merges/{srcBranch}-to-{destBranch}";

            // Check to see if there's already a PR from source to destination branch
            // https://developer.github.com/v3/pulls/#list-pull-requests
            HttpResponseMessage prsResponse = await _client.GetAsync(
                $"repos/{repoOwner}/{repoName}/pulls?state=open&base={destBranch}&head={repoOwner}:{prBranchName}");

            if (!prsResponse.IsSuccessStatusCode)
            {
                return (false, prsResponse);
            }

            var existingPrData = JsonSerializer.Deserialize<ExistingPrItem[]>(
                await prsResponse.Content.ReadAsStringAsync(), s_jsonOptions)
                ?.FirstOrDefault(pr => pr.title == prTitle);

            if (existingPrData != null)
            {
                if (updateExistingPr)
                {
                    // Get the SHA of the PR branch HEAD
                    var prSha = existingPrData.Head.Sha;
                    var existingPrNumber = existingPrData.Number;

                    // Check for merge conflicts
                    var existingPrConflicted = await IsPrConflicted(existingPrNumber);

                    // Only update PR w/o merge conflicts
                    if (existingPrConflicted == false && prSha != srcSha)
                    {
                        Console.WriteLine("Updating existing PR.");

                        // Try to reset the HEAD of PR branch to latest source branch
                        response = await ResetBranch(prBranchName, srcSha, force: false);
                        if (!response.IsSuccessStatusCode)
                        {
                            Console.WriteLine($"There's additional change in `{srcBranch}` but an attempt to fast-forward `{prBranchName}` failed.");
                            return (false, response);
                        }

                        await PostComment(existingPrNumber, $"Reset HEAD of `{prBranchName}` to `{srcSha}`");

                        // Check for merge conflicts again after reset.
                        existingPrConflicted = await IsPrConflicted(existingPrNumber);
                    }

                    // Add label if there's merge conflicts even if we made no change to merge branch,
                    // since can also be introduced by change in destination branch. It's no-op if the
                    // label already exists.
                    if (existingPrConflicted == true)
                    {
                        Console.WriteLine("PR has merge conflicts. Adding Merge Conflicts label.");
                        await AddLabels(existingPrNumber, new List<string> { MergeConflictsLabelText });
                    }
                }

                return (false, null);
            }

            Console.WriteLine("Creating branch");

            // Create a PR branch on the repo
            // https://developer.github.com/v3/git/refs/#create-a-reference
            response = await _client.PostAsyncAsJson($"repos/{repoOwner}/{repoName}/git/refs",
                JsonSerializer.Serialize(new { @ref = $"refs/heads/{prBranchName}", sha = srcSha }));

            if (response.StatusCode != HttpStatusCode.Created)
            {
                // PR branch already exists. Hard reset to the new SHA
                if (response.StatusCode == HttpStatusCode.UnprocessableEntity)
                {
                    response = await ResetBranch(prBranchName, srcSha, force: true);
                    if (!response.IsSuccessStatusCode)
                    {
                        return (false, response);
                    }
                }
                else
                {
                    return (false, response);
                }
            }

            string autoTriggeredMessage = isAutoTriggered ? "" : "(created from a manual run of the PR generation tool)";

            var prMessage = $@"
This is an automatically generated pull request from {srcBranch} into {destBranch}.
{autoTriggeredMessage}

Once all conflicts are resolved and all the tests pass, you are free to merge the pull request. 🐯

## Troubleshooting conflicts

### Identify authors of changes which introduced merge conflicts
Scroll to the bottom, then for each file containing conflicts copy its path into the following searches:
- https://github.com/{repoOwner}/{repoName}/find/{srcBranch}
- https://github.com/{repoOwner}/{repoName}/find/{destBranch}

Usually the most recent change to a file between the two branches is considered to have introduced the conflicts, but sometimes it will be necessary to look for the conflicting lines and check the blame in each branch. Generally the author whose change introduced the conflicts should pull down this PR, fix the conflicts locally, then push up a commit resolving the conflicts.

### Resolve merge conflicts using your local repo
Sometimes merge conflicts may be present on GitHub but merging locally will work without conflicts. This is due to differences between the merge algorithm used in local git versus the one used by GitHub.
``` bash
git fetch --all
git checkout -t upstream/{prBranchName}
git reset --hard upstream/{destBranch}
git merge upstream/{srcBranch}
# Fix merge conflicts
git commit
git push upstream {prBranchName} --force
```
";

            Console.WriteLine("Creating PR");

            // Create a PR from the new branch to the dest
            // https://developer.github.com/v3/pulls/#create-a-pull-request
            response = await _client.PostAsyncAsJson($"repos/{repoOwner}/{repoName}/pulls",
                JsonSerializer.Serialize(new { title = prTitle, body = prMessage, head = prBranchName, @base = destBranch }));

            // 422 (Unprocessable Entity) indicates there were no commits to merge
            if (response.StatusCode == HttpStatusCode.UnprocessableEntity)
            {
                // Delete the pr branch if the PR was not created.
                // https://developer.github.com/v3/git/refs/#delete-a-reference
                await _client.DeleteAsync($"repos/{repoOwner}/{repoName}/git/refs/heads/{prBranchName}");
                return (false, null);
            }

            var createPrData = JsonSerializer.Deserialize<CreatePrResponse>(
                await response.Content.ReadAsStringAsync(), s_jsonOptions);

            var prNumber = createPrData.Number;
            var prNodeId = createPrData.NodeId;
            var hasConflicts = !createPrData.Mergeable;

            if (hasConflicts == null)
            {
                hasConflicts = await IsPrConflicted(prNumber);
            }

            var labels = new List<string> { AreaInfrastructureLabelText };
            if (addAutoMergeLabel)
            {
                labels.Add(AutoMergeLabelText);
            }

            if (hasConflicts == true)
            {
                Console.WriteLine("PR has merge conflicts. Adding Merge Conflicts label.");
                labels.Add(MergeConflictsLabelText);
            }

            // Add labels to the issue
            response = await AddLabels(prNumber, labels);

            // Add assignees to the issue
            if (prOwners.Any())
            {
                Console.WriteLine("Adding assignees: " + string.Join(", ", prOwners));
                foreach (var owner in prOwners)
                {
                    if (await IsInvalidAssignee(owner))
                    {
                        Console.WriteLine($"##vso[task.logissue type=warning]{repoOwner}/{repoName}:{prBranchName} has invalid owner \"{owner}\".");
                    }
                }

                response = await AddAssignees(prNumber, prOwners);
                var assigneeData = JsonSerializer.Deserialize<AssigneeResponse>(
                    await response.Content.ReadAsStringAsync(), s_jsonOptions);
                Console.WriteLine("Actual assignees: " + (assigneeData.Assignees.Any() ? string.Join(", ", assigneeData.Assignees.Select(a => a.Login)) : "(none)"));

                if (hasConflicts == true)
                {
                    response = await PostComment(prNumber, "⚠ This PR has merge conflicts. " + string.Join(" ", prOwners.Select(owner => "@" + owner)));
                }
            }

            if (!response.IsSuccessStatusCode)
            {
                return (true, response);
            }

            // https://docs.github.com/en/graphql/reference/mutations#enablepullrequestautomerge
            response = await _client.PostAsyncAsJson("https://api.github.com/graphql",
                JsonSerializer.Serialize(new
                {
                    query = @"
mutation ($pullRequestId: ID!, $mergeMethod: PullRequestMergeMethod!) {
  enablePullRequestAutoMerge(input: {
    pullRequestId: $pullRequestId,
    mergeMethod: $mergeMethod
  }) {
    pullRequest {
      autoMergeRequest {
        enabledAt
        enabledBy {
          login
        }
      }
    }
  }
}",
                    variables = new
                    {
                        pullRequestId = prNodeId,
                        mergeMethod = "MERGE"
                    }
                }));

            var enableAutoMergeData = JsonSerializer.Deserialize<AutoMergeResponse>(
                await response.Content.ReadAsStringAsync(), s_jsonOptions);

            if (enableAutoMergeData.Errors is { } errors)
            {
                Console.WriteLine("Failed to enable automerge on PR.");
                foreach (var err in errors)
                {
                    Console.WriteLine(err.Message);
                }
            }

            return (true, null);

            Task<HttpResponseMessage> ResetBranch(string branchName, string sha, bool force)
            {
                Console.WriteLine($"Resetting branch {branchName}");

                // https://developer.github.com/v3/git/refs/#update-a-reference
                var body = JsonSerializer.Serialize(new { sha, force });
                return _client.PostAsyncAsJson($"repos/{repoOwner}/{repoName}/git/refs/heads/{branchName}", body);
            }

            Task<HttpResponseMessage> PostComment(string prNumber, string comment)
            {
                // https://developer.github.com/v3/pulls/comments/#create-a-comment
                var body = JsonSerializer.Serialize(new { body = comment });
                return _client.PostAsyncAsJson($"repos/{repoOwner}/{repoName}/issues/{prNumber}/comments", body);
            }

            async Task<bool?> IsPrConflicted(string prNumber, int maxAttempts = 5)
            {
                var attempt = 0;
                bool? prHasConflicts = null;

                Console.Write("Waiting for mergeable status");
                while (prHasConflicts == null && attempt < maxAttempts)
                {
                    attempt++;
                    Console.Write(".");
                    await Task.Delay(1000);

                    // Get the pull request
                    // https://developer.github.com/v3/pulls/#get-a-single-pull-request
                    var response = await _client.GetAsync($"repos/{repoOwner}/{repoName}/pulls/{prNumber}");
                    var data = JsonSerializer.Deserialize<PrStatusResponse>(
                        await response.Content.ReadAsStringAsync(), s_jsonOptions);

                    if (data.Mergeable is null)
                    {
                        // GitHub is still computing the mergeability of this PR
                        continue;
                    }

                    // "dirty" indicated merge conflicts causing the mergeability to be false
                    // see https://github.community/t5/How-to-use-Git-and-GitHub/API-Getting-the-reason-that-a-pull-request-isn-t-mergeable/td-p/5796
                    var hasMergeConflicts = data.Mergeable == false && data.MergeableState == "dirty";

                    // treat the presense of a merge conflict label as unmergeable so that we do not
                    // update a corrected PR with new merge conflicts
                    var hasMergeConflictsLabel = data.Labels.Select(label => label.Name).Contains(MergeConflictsLabelText);

                    prHasConflicts = hasMergeConflicts || hasMergeConflictsLabel;
                }

                Console.WriteLine();

                if (prHasConflicts == null)
                {
                    Console.WriteLine($"##vso[task.logissue type=warning]Timed out waiting for PR mergeability status to become available.");
                }

                return prHasConflicts;
            }

            Task<HttpResponseMessage> AddLabels(string prNumber, List<string> labels)
            {
                // https://developer.github.com/v3/issues/labels/#add-labels-to-an-issue
                return _client.PostAsyncAsJson($"repos/{repoOwner}/{repoName}/issues/{prNumber}/labels",
                    JsonSerializer.Serialize(labels));
            }

            Task<HttpResponseMessage> AddAssignees(string prNumber, List<string> assignees)
            {
                // https://developer.github.com/v3/issues/assignees/#add-assignees-to-an-issue
                return _client.PostAsyncAsJson($"repos/{repoOwner}/{repoName}/issues/{prNumber}/assignees",
                    JsonSerializer.Serialize(new { assignees }));
            }

            async Task<bool> IsInvalidAssignee(string assignee)
            {
                // https://developer.github.com/v3/issues/assignees/#check-assignee
                var response = await _client.GetAsync($"repos/{repoOwner}/{repoName}/assignees/{assignee}");
                return response.StatusCode == HttpStatusCode.NotFound;
            }
        }

        public async Task<(IReadOnlyList<MergePr> MergePrs, HttpResponseMessage Response)> FetchOpenMergePRsAsync(string repoOwner, string repoName)
        {
            List<MergePr> mergePRs = new List<MergePr>();

            // Check to see if there's open merge prs
            // https://developer.github.com/v3/search/#search-issues-and-pull-requests
            HttpResponseMessage prsResponse = await _client.GetAsync(
                $"/search/issues?q=repo:{repoOwner}/{repoName}+is:open+is:pr+label:{AutoMergeLabelText}");

            if (!prsResponse.IsSuccessStatusCode)
            {
                return (mergePRs, prsResponse);
            }

            var possibleMergePrs = JsonSerializer.Deserialize<SearchResponse>(
                await prsResponse.Content.ReadAsStringAsync(), s_jsonOptions);

            foreach (var possibleMergePr in possibleMergePrs.Items)
            {
                var match = MergePrTitlePattern.Match(possibleMergePr.Title);
                if (!match.Success)
                {
                    continue;
                }

                mergePRs.Add(new MergePr()
                {
                    Number = possibleMergePr.Number,
                    SrcBranch = match.Groups[1].Value,
                    DestBranch = match.Groups[2].Value
                });
            }

            return (mergePRs, prsResponse);
        }

        public class MergePr
        {
            public int Number { get; set; }
            public string SrcBranch { get; set; }
            public string DestBranch { get; set; }
        }

        private static Regex MergePrTitlePattern => new Regex(@"^Merge (.*) to (.*)", RegexOptions.Compiled);

        public const string AutoMergeLabelText = "auto-merge";
        public const string MergeConflictsLabelText = "Merge Conflicts";
        public const string AreaInfrastructureLabelText = "Area-Infrastructure";
    }
}

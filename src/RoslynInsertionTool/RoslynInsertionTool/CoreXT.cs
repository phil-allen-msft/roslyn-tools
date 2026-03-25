// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the License.txt file in the project root for more information.

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Xml.XPath;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.VisualStudio.Services.Common;
using NuGet.Versioning;

namespace Roslyn.Insertion
{
    internal class CoreXT
    {
        private static Dictionary<string, string> ComponentToFileMap = null!;
        private static Dictionary<string, (string original, ComponentsJson document)> ComponentFileToDocumentMap = null!;
        private static HashSet<string> dirtyComponentFiles = null!;

        /// <summary>
        /// A map that maps from the package name to the list of prop files that specify the package version.
        /// </summary>
        private static Dictionary<string, ICollection<string>> PackageToPropFilesMap = null!;

        /// <summary>
        /// A map from a property file name to the original content and parsed XML document.
        /// </summary>
        private static Dictionary<string, (string original, XDocument document)> PackagePropFileToDocumentMap = null!;
        private static HashSet<string> dirtyPropsFiles = null!;

        private const string DefaultConfigPath = ".corext/Configs/default.config";
        private const string ComponentsJsonPath = ".corext/Configs/components.json";
        private const string PackagePropsPath = "Directory.Packages.props";
        private const string LegacyPackagePropsPath = "Packages.props";
        private const string XamlVersionPropsPath = "/src/Xaml/Versions.props";

        private readonly string? _defaultConfigOriginal;

        public XDocument? ConfigDocument { get; }

        private CoreXT(string? configOriginalText)
        {
            _defaultConfigOriginal = configOriginalText;
            ConfigDocument = configOriginalText is null ? null : XDocument.Parse(configOriginalText, LoadOptions.None);
        }

        public static async Task<CoreXT> Load(GitHttpClient gitClient, string commitId)
        {
            var vsRepoId = RoslynInsertionTool.VSRepoId;
            var vsBranch = new GitVersionDescriptor { VersionType = GitVersionType.Commit, Version = commitId };

            string? defaultConfigOriginal;
            try
            {
                using var defaultConfigStream = await gitClient.GetItemContentAsync(
                    vsRepoId,
                    DefaultConfigPath,
                    download: true,
                    versionDescriptor: vsBranch);
                defaultConfigOriginal = await new StreamReader(defaultConfigStream).ReadToEndAsync();
                Console.WriteLine($"'{DefaultConfigPath}' found.");
            }
            catch (VssServiceException ex) when (ex.IsFileNotFound())
            {
                defaultConfigOriginal = null;
                Console.WriteLine($"'{DefaultConfigPath}' not found. Will search package props files for current versions instead.");
            }

            ComponentToFileMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            ComponentFileToDocumentMap = new Dictionary<string, (string, ComponentsJson)>(StringComparer.OrdinalIgnoreCase);
            dirtyComponentFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            PackageToPropFilesMap = new Dictionary<string, ICollection<string>>(StringComparer.OrdinalIgnoreCase);
            PackagePropFileToDocumentMap = new Dictionary<string, (string original, XDocument document)>(StringComparer.OrdinalIgnoreCase);
            dirtyPropsFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            await PopulateComponentJsonMaps(gitClient, commitId);
            await PopulatePackagePropFileMaps(gitClient, commitId);

            return new CoreXT(defaultConfigOriginal);
        }

        public IEnumerable<GitChange?> SaveConfigs()
        {
            yield return RoslynInsertionTool.GetChangeOpt(DefaultConfigPath, _defaultConfigOriginal, toFullString(_defaultConfigOriginal, ConfigDocument));

            foreach (var propFile in dirtyPropsFiles)
            {
                (string? original, XDocument? document) pair = (null, null);
                if (PackagePropFileToDocumentMap.TryGetValue(propFile, out pair!))
                {
                    yield return RoslynInsertionTool.GetChangeOpt(propFile, pair.original, toFullString(pair.original, pair.document));
                }
            }

            static string? toFullString(string? original, XDocument? document)
            {
                if (original is null || document is null)
                {
                    return null;
                }

                var documentString = document.Declaration switch
                {
                    null => "",
                    var decl => decl.ToString() + "\n"
                } + document.ToString();
                if (original.EndsWith("\n"))
                {
                    documentString += "\n";
                }
                return documentString;
            }
        }

        public List<GitChange> SaveComponents()
        {
            var changes = new List<GitChange>();
            foreach (var kvp in ComponentFileToDocumentMap)
            {
                if (dirtyComponentFiles.Contains(kvp.Key))
                {
                    var (original, doc) = kvp.Value;
                    if (doc is null)
                    {
                        continue;
                    }

                    // Preserve trailing newline if present
                    var newText = JsonSerializer.Serialize(doc, s_writeIndentedOptions) + (original.EndsWith("\n") ? "\n" : "");
                    if (RoslynInsertionTool.GetChangeOpt(kvp.Key, original, newText) is GitChange change)
                    {
                        changes.Add(change);
                    }
                }
            }

            return changes;
        }

        private XAttribute? GetDefaultConfigVersionAttribute(PackageInfo packageInfo)
        {
            return ConfigDocument?.Root
                .Elements("packages")
                .Elements("package")
                .Where(p => p.Attribute("id")?.Value == packageInfo.PackageName)
                .Select(x => x.Attribute("version")).SingleOrDefault();
        }

        // Try to get a version number from a props file under /src/ConfigData/Packages.
        private string? GetVersionStringInPropsFile(PackageInfo packageInfo)
        {
            if (!PackageToPropFilesMap.TryGetValue(packageInfo.PackageName, out var propsFiles))
            {
                return null;
            }

            var propsFile = propsFiles.First(); // assume if multiple props files then they use the same version
            if (!PackagePropFileToDocumentMap.TryGetValue(propsFile, out var textAndDocument))
            {
                return null;
            }

            var attributeOrElement = GetVersionAttributeOrElementInPropsFile(textAndDocument.document!, packageInfo);
            if (attributeOrElement is null)
                return null;

            if (attributeOrElement is XAttribute attribute)
                return attribute.Value;

            var element = (XElement)attributeOrElement;
            return element.Attribute("Version")?.Value ?? element.Value;
        }

        /// <summary>
        /// Returns the appropriate element that contains the version of the specified package. Will return either a <see cref="XAttribute" /> or <see cref="XElement"/> depending.
        /// </summary>
        private static XObject? GetVersionAttributeOrElementInPropsFile(XDocument document, PackageInfo packageInfo)
        {
            // Support both the existing tags - "PackageVersion" and "Include" as well the legacy tags "PackageReference" and "Update" so we could still update servicing branches.
            var attribute = document.Root
                .Elements().Where(e => string.Equals(e.Name.LocalName, "ItemGroup"))
                .Elements().Where(e => string.Equals(e.Name.LocalName, "PackageVersion") || string.Equals(e.Name.LocalName, "PackageReference"))
                .Where(p => p.Attribute("Include")?.Value == packageInfo.PackageName || p.Attribute("Update")?.Value == packageInfo.PackageName)
                .Select(x => x.Attribute("Version")).SingleOrDefault();

            if (attribute is null)
                return null;

            // If the attribute is a property substitution, then we'll see if we actually can find the property itself
            if (attribute.Value.StartsWith("$(") && attribute.Value.EndsWith(")"))
            {
                var propertyName = attribute.Value.Substring(2, attribute.Value.Length - 3);
                var versionProperty = document.Root.Elements("PropertyGroup").Elements(propertyName).SingleOrDefault();

                if (versionProperty is not null)
                    return versionProperty;
            }

            return attribute;
        }

        private XElement? GetClosestFollowingPackageElement(PackageInfo packageInfo)
        {
            return ConfigDocument?.Root.
                Elements("packages").
                Elements("package").
                FirstOrDefault(p => StringComparer.OrdinalIgnoreCase.Compare(packageInfo.PackageName, p.Attribute("id").Value) < 0);
        }

        public void UpdatePackageVersion(PackageInfo packageInfo)
        {
            var versionAttribute = GetDefaultConfigVersionAttribute(packageInfo);
            versionAttribute?.SetValue(packageInfo.Version.ToString());

            if (PackageToPropFilesMap.TryGetValue(packageInfo.PackageName, out var propFiles))
            {
                foreach (var file in propFiles)
                {
                    (string? original, XDocument? document) pair = (null, null);
                    if (PackagePropFileToDocumentMap.TryGetValue(file, out pair!))
                    {
                        var versionObject = GetVersionAttributeOrElementInPropsFile(pair.document!, packageInfo);
                        if (versionObject is XAttribute attribute)
                            attribute.SetValue(packageInfo.Version.ToString());
                        else if (versionObject is XElement element)
                            element.SetValue(packageInfo.Version.ToString());
                        else if (versionObject is not null)
                            throw new Exception($"Unexpected type of object returned from {nameof(GetVersionAttributeOrElementInPropsFile)}: {versionObject.GetType().FullName}");

                        dirtyPropsFiles.Add(file);
                    }
                }
            }

            // Update <XamlRoslynVersion> inside /src/Xaml/Versions.props.
            if (RoslynInsertionTool.Options.UpdateXamlRoslynVersion &&
                packageInfo.IsRoslyn &&
                PackagePropFileToDocumentMap.TryGetValue(XamlVersionPropsPath, out var xamlProps) &&
                xamlProps.document.XPathSelectElements("//*[local-name()='XamlRoslynVersion']").FirstOrDefault() is { } xamlRoslynVersionElement)
            {
                xamlRoslynVersionElement.Value = packageInfo.Version.ToString();
                dirtyPropsFiles.Add(XamlVersionPropsPath);
            }
        }

        public bool TryGetPackageVersion(PackageInfo packageInfo, out NuGetVersion? version)
        {
            if (GetDefaultConfigVersionAttribute(packageInfo) is { } attribute)
            {
                version = NuGetVersion.Parse(attribute.Value);
                return true;
            }

            if (GetVersionStringInPropsFile(packageInfo) is { } versionString)
            {
                version = NuGetVersion.Parse(versionString);
                return true;
            }

            version = null;
            return false;
        }

        public bool TryGetComponentByName(string componentName, out Component? component)
        {
            component = null;

            (_, ComponentsJson? componentDocument) = GetJsonDocumentForComponent(componentName);

            if (componentDocument?.Components == null)
            {
                return false;
            }

            if (!componentDocument.Components.TryGetValue(componentName, out var entry) || entry == null)
            {
                return false;
            }

            if (string.IsNullOrEmpty(entry.FileName) || string.IsNullOrEmpty(entry.Url))
            {
                return false;
            }

            var componentUri = new Uri(entry.Url);
            component = new Component(componentName, entry.FileName, componentUri, entry.Version);
            return true;
        }

        public void UpdateComponent(Component component)
        {
            var (_, componentDocument) = GetJsonDocumentForComponent(component.Name);

            if (componentDocument?.Components == null)
            {
                return;
            }

            if (!componentDocument.Components.TryGetValue(component.Name, out var entry) || entry == null)
            {
                return;
            }

            entry.FileName = component.Filename;
            entry.Url = component.Uri.ToString();
            // Version is [JsonIgnore(WhenWritingNull)] so setting to null removes it from output
            entry.Version = component.Version;

            string componentFilePath = ComponentToFileMap[component.Name];
            dirtyComponentFiles.Add(componentFilePath);
        }

        private static async Task PopulateComponentJsonMaps(
            GitHttpClient gitClient,
            string commitId)
        {
            var (mainOriginal, mainComponentsJsonDocument) = await GetJsonDocumentForComponentsFile(gitClient, commitId, ComponentsJsonPath);
            if (mainComponentsJsonDocument != null)
            {
                ComponentFileToDocumentMap[ComponentsJsonPath] = (mainOriginal, mainComponentsJsonDocument);
                PopulateComponentToFileMapForFile(mainComponentsJsonDocument, ComponentsJsonPath);

                // Process sub components.json
                var imports = mainComponentsJsonDocument.Imports;
                if (imports != null)
                {
                    foreach (var subComponentFileName in imports)
                    {

                        if (!string.IsNullOrEmpty(subComponentFileName))
                        {
                            var componentsJSONPath = ".corext/Configs/" + subComponentFileName;
                            var (original, jDoc) = await GetJsonDocumentForComponentsFile(gitClient, commitId, componentsJSONPath);

                            if (jDoc != null && !ComponentFileToDocumentMap.ContainsKey(componentsJSONPath))
                            {
                                ComponentFileToDocumentMap[componentsJSONPath] = (original, jDoc);
                                PopulateComponentToFileMapForFile(jDoc, componentsJSONPath);
                            }
                        }
                    }
                }
            }
        }

        private static void PopulateComponentToFileMapForFile(ComponentsJson jDocument, string componentsJsonFileName)
        {
            if (jDocument?.Components != null && componentsJsonFileName != null)
            {
                foreach (var kvp in jDocument.Components)
                {
                    if (!ComponentToFileMap.ContainsKey(kvp.Key))
                    {
                        ComponentToFileMap[kvp.Key] = componentsJsonFileName;
                    }
                }
            }
        }

        private static async Task<(string original, ComponentsJson document)> GetJsonDocumentForComponentsFile(
            GitHttpClient gitClient,
            string commitId,
            string componentsJSONPath)
        {
            var versionDescriptor = new GitVersionDescriptor { VersionType = GitVersionType.Commit, Version = commitId };
            try
            {
                using var fileStream = await gitClient.GetItemContentAsync(RoslynInsertionTool.VSRepoId, path: componentsJSONPath, versionDescriptor: versionDescriptor);
                var original = await new StreamReader(fileStream).ReadToEndAsync();
                var jsonDocument = JsonSerializer.Deserialize<ComponentsJson>(original, s_componentsOptions);
                return (original, jsonDocument);
            }
            catch (Exception e)
            {
                throw new IOException($"Unable to load file from '{componentsJSONPath}'", e);
            }
        }

        private (string? original, ComponentsJson? document) GetJsonDocumentForComponent(string componentName)
        {
            (string?, ComponentsJson?) pair = (null, null);

            if (!string.IsNullOrEmpty(componentName))
            {
                string componentFileName;
                if (ComponentToFileMap.TryGetValue(componentName, out componentFileName))
                {
                    // ValueTuple is not covariant so we need to suppress the warning on 'pair'
                    ComponentFileToDocumentMap.TryGetValue(componentFileName, out pair!);
                }
            }

            return pair;
        }

        private static readonly JsonSerializerOptions s_componentsOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        private static readonly JsonSerializerOptions s_writeIndentedOptions = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        private sealed class ComponentsJson
        {
            [JsonPropertyName("Imports")]
            public string[] Imports { get; set; }

            [JsonPropertyName("Components")]
            public Dictionary<string, ComponentEntry> Components { get; set; }

            [JsonExtensionData]
            public Dictionary<string, JsonElement> AdditionalProperties { get; set; }
        }

        private sealed class ComponentEntry
        {
            [JsonPropertyName("fileName")]
            public string FileName { get; set; }

            [JsonPropertyName("url")]
            public string Url { get; set; }

            [JsonPropertyName("version")]
            [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
            public string Version { get; set; }

            [JsonExtensionData]
            public Dictionary<string, JsonElement> AdditionalProperties { get; set; }
        }

        private static async Task<bool> IsFilePresentAsync(
            GitHttpClient gitClient,
            GitVersionDescriptor versionDescriptor,
            string filePath)
        {
            try
            {
                var packagePropsItem = await gitClient.GetItemsAsync(RoslynInsertionTool.VSRepoId,
                    scopePath: filePath,
                    recursionLevel: VersionControlRecursionType.Full,
                    download: true,
                    versionDescriptor: versionDescriptor);
                return packagePropsItem.Any();
            }
            catch
            {
                return false;
            }
        }

        private static async Task PopulatePackagePropFileMaps(
            GitHttpClient gitClient,
            string commitId)
        {
            var versionDescriptor = new GitVersionDescriptor { VersionType = GitVersionType.Commit, Version = commitId };

            var packagePropsFile = await IsFilePresentAsync(gitClient, versionDescriptor, PackagePropsPath)
                ? PackagePropsPath
                : LegacyPackagePropsPath;

            await ProcessPropsPath(packagePropsFile, versionDescriptor);

            if (RoslynInsertionTool.Options.UpdateXamlRoslynVersion)
            {
                await ProcessPropsPath(XamlVersionPropsPath, versionDescriptor);
            }

            if (PackagePropFileToDocumentMap.Any())
            {
                var parentPackagesDoc = PackagePropFileToDocumentMap.First().Value.document;

                var importedPropPaths = parentPackagesDoc.Root.Elements().Where(e => string.Equals(e.Name.LocalName, "Import"))
                    .Select(e => e.Attributes("Project").FirstOrDefault()?.Value);

                foreach (var propPath in importedPropPaths)
                {
                    if (propPath == null) continue;

                    if (propPath.EndsWith(@"\**\*.props"))
                    {
                        await ProcessPropsPath(propPath.Substring(0, propPath.IndexOf(@"\**\*.props")), versionDescriptor);
                    }
                    else if (propPath.EndsWith(@"\*.props"))
                    {
                        await ProcessPropsPath(propPath.Substring(0, propPath.IndexOf(@"\*.props")), versionDescriptor);
                    }
                    else
                    {
                        await ProcessPropsPath(propPath, versionDescriptor);
                    }
                }
            }

            async Task ProcessPropsPath(string propsPath, GitVersionDescriptor versionDescriptor)
            {
                var propsFiles = await gitClient.GetItemsAsync(RoslynInsertionTool.VSRepoId,
                    scopePath: propsPath,
                    recursionLevel: VersionControlRecursionType.Full,
                    download: true,
                    versionDescriptor: versionDescriptor);

                foreach (var item in propsFiles)
                {
                    if (item.IsFolder || item.IsSymbolicLink)
                    {
                        continue;
                    }

                    try
                    {
                        using var fileStream = await gitClient.GetItemContentAsync(RoslynInsertionTool.VSRepoId, path: item.Path, versionDescriptor: versionDescriptor);
                        var content = await new StreamReader(fileStream).ReadToEndAsync();
                        var (original, document) = (content, XDocument.Parse(content));
                        if (document != null && !PackagePropFileToDocumentMap.ContainsKey(item.Path))
                        {
                            PackagePropFileToDocumentMap[item.Path] = (original, document);
                        }

                        PopulatePackageToPropFileMap(document!, item.Path);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Unable to parse file {item.Path}");
                        Console.WriteLine(ex.Message);
                    }
                }
            }
        }

        private static void PopulatePackageToPropFileMap(XDocument document, string propFileName)
        {
            try
            {
                // "PackageReference" and "Update" tags are the legacy nomenclature that we still need to support for servicing branches.
                var packageRefs = document.Root
                    .Elements().Where(e => string.Equals(e.Name.LocalName, "ItemGroup"))
                    .Elements().Where(e => string.Equals(e.Name.LocalName, "PackageVersion") || string.Equals(e.Name.LocalName, "PackageReference"));

                foreach (var packageRef in packageRefs)
                {
                    var name = packageRef.Attribute("Include")?.Value;
                    if (string.IsNullOrEmpty(name))
                    {
                        // Try the legacy nomenclature
                        name = packageRef.Attribute("Update")?.Value;
                    }

                    if (!string.IsNullOrEmpty(name))
                    {
                        if (!PackageToPropFilesMap.ContainsKey(name!))
                        {
                            PackageToPropFilesMap[name!] = new List<string>();
                        }

                        PackageToPropFilesMap[name!].Add(propFileName);
                    }
                }
            }
            catch (Exception ex)
            {
                // Swallow exceptions reading any of these files.
                Console.WriteLine($"Could not load contents of {propFileName}");
                Console.WriteLine(ex.Message);
            }
        }
    }
}

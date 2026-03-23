// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the License.txt file in the project root for more information.

using System.Text.Json.Serialization;

namespace SignTool.Json
{
    internal sealed class FileJson
    {
        [JsonPropertyName("kind")]
        public string Kind { get; set; }

        [JsonPropertyName("publishUrl")]
        public string PublishUrl { get; set; }

        [JsonPropertyName("sign")]
        public FileSignData[] SignList { get; set; }

        [JsonPropertyName("exclude")]
        public string[] ExcludeList { get; set; }

        public FileJson()
        {

        }
    }

    internal sealed class OrchestratedFileJson
    {
        [JsonPropertyName("kind")]
        public string Kind { get; set; }

        [JsonPropertyName("sign")]
        public OrchestratedFileSignData[] SignList { get; set; }

        [JsonPropertyName("exclude")]
        public string[] ExcludeList { get; set; }

        public OrchestratedFileJson()
        {
        }
    }

    internal class FileSignDataBase
    {
        [JsonPropertyName("certificate")]
        public string Certificate { get; set; }

        [JsonPropertyName("strongName")]
        public string StrongName { get; set; }
    }


    internal sealed class FileSignData : FileSignDataBase
    {
        [JsonPropertyName("values")]
        public string[] FileList { get; set; }

        public FileSignData()
        {
        }
    }

    internal sealed class OrchestratedFileSignData : FileSignDataBase
    {
        [JsonPropertyName("values")]
        public FileSignDataEntry[] FileList { get; set; }

        public OrchestratedFileSignData()
        {
        }
    }

    internal sealed class FileSignDataEntry
    {
        [JsonPropertyName("filePath")]
        public string FilePath { get; set; }

        [JsonPropertyName("sha256Hash")]
        public string SHA256Hash { get; set; }

        [JsonPropertyName("publishtofeedurl")]
        public string PublishToFeedUrl { get; set; }

        public FileSignDataEntry()
        {
        }
    }
}

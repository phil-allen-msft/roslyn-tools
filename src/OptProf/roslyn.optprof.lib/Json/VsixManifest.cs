// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the License.txt file in the project root for more information.

using System.Text.Json.Serialization;

namespace roslyn.optprof.lib
{
    public sealed class VsixManifest
    {
        [JsonPropertyName("extensionDir")]
        public string ExtensionDir { get; set; }

        [JsonPropertyName("files")]
        public VsixManifestFile[] Files { get; set; }
    }

    public sealed class VsixManifestFile
    {
        [JsonPropertyName("fileName")]
        public string FileName { get; set; }

        [JsonPropertyName("ngen")]
        public bool? Ngen { get; set; }

        [JsonPropertyName("ngenPriority")]
        public int? NgenPriority { get; set; }

        [JsonPropertyName("ngenArchitecture")]
        public string NgenArchitecture { get; set; }

        [JsonPropertyName("ngenApplication")]
        public string NgenApplication { get; set; }
    }
}

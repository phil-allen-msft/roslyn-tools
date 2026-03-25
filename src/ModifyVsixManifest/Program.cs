// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the License.txt file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Packaging;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml.Linq;
using Mono.Options;

namespace ModifyVsixManifest
{
    internal class Program
    {
        public static void Main(string[] args)
        {
            string vsixName = null;
            var operations = new List<IVsixManifestOperation>();
            var parameters = new OptionSet()
            {
                @"Usage: {exename} --vsix=path\to\package.vsix [options]",
                "",
                "Options:",
                { "vsix=", "The VSIX package to modify.", value => vsixName = value },
                { "add-attribute=", "The XPath of the parent to the attribute, the attribute name, and the value to add, all separated by semicolons.", value => operations.Add(AddVsixValueOperation.FromSemicolonDelimited(value)) },
                { "remove=", "The XPath of the value to remove.", path => operations.Add(new RemoveVsixValueOperation(path)) }
            };

            if (args.Length == 0)
            {
                parameters.WriteOptionDescriptions(Console.Out);
                Environment.Exit(0);
            }

            try
            {
                parameters.Parse(args);
            }
            catch (OptionException)
            {
                parameters.WriteOptionDescriptions(Console.Out);
                Environment.Exit(1);
            }

            using (var package = Package.Open(vsixName))
            {
                var (name, hash) = UpdateExtensionVsixManifest(package, operations);
                UpdatePartHashInManifestJson(package, name, hash);
            }
        }

        private static (string name, byte[] hash) UpdateExtensionVsixManifest(Package package, List<IVsixManifestOperation> operations)
        {
            var partName = "/extension.vsixmanifest";
            var part = package.GetPart(new Uri(partName, UriKind.Relative));

            byte[] hash;
            using (var stream = part.GetStream(FileMode.Open))
            {
                var document = XDocument.Load(stream);
                foreach (var operation in operations)
                {
                    operation.Execute(document);
                }

                using (var newContent = new MemoryStream())
                {
                    document.Save(newContent);

                    // overwrite the content of the part in VSIX:
                    stream.Seek(0, SeekOrigin.Begin);
                    stream.SetLength(newContent.Length);
                    newContent.Seek(0, SeekOrigin.Begin);
                    newContent.CopyTo(stream);

                    // calculate new hash:
                    newContent.Seek(0, SeekOrigin.Begin);
                    using (var sha = SHA256.Create())
                    {
                        hash = sha.ComputeHash(newContent);
                    }
                }
            }

            return (partName, hash);
        }

        private static void UpdatePartHashInManifestJson(Package package, string partName, byte[] partHash)
        {
            var part = package.GetPart(new Uri("/manifest.json", UriKind.Relative));

            using (var stream = part.GetStream(FileMode.Open))
            {
                string jsonStr;
                using (var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 2048, leaveOpen: true))
                {
                    jsonStr = reader.ReadToEnd();
                }

                var manifest = JsonSerializer.Deserialize<ManifestDocument>(jsonStr);

                var file = manifest.Files.Single(f => f.FileName == partName);
                file.Sha256 = BitConverter.ToString(partHash).Replace("-", "");

                stream.Position = 0;
                stream.SetLength(0);

                using (var writer = new StreamWriter(stream, Encoding.UTF8, bufferSize: 2048, leaveOpen: false))
                {
                    writer.Write(JsonSerializer.Serialize(manifest));
                }
            }
        }

        private sealed class ManifestDocument
        {
            [JsonPropertyName("files")]
            public ManifestFileEntry[] Files { get; set; }

            [JsonExtensionData]
            public Dictionary<string, JsonElement> AdditionalProperties { get; set; }
        }

        private sealed class ManifestFileEntry
        {
            [JsonPropertyName("fileName")]
            public string FileName { get; set; }

            [JsonPropertyName("sha256")]
            public string Sha256 { get; set; }

            [JsonExtensionData]
            public Dictionary<string, JsonElement> AdditionalProperties { get; set; }
        }
    }
}

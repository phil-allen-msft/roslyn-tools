// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the License.txt file in the project root for more information.

using System;
using System.IO;
using System.Linq;
using Xunit;

namespace SignTool.Json.Tests
{
    public class JsonReadingTests
    {
        #region TryReadConfigFile

        private static BatchSignInput LoadConfigFile(string json)
        {
            using (var reader = new StringReader(json))
            using (var writer = new StringWriter())
            {
                Assert.True(Program.TryReadConfigFile(writer, reader, @"q:\outputPath", out var data));
                Assert.True(string.IsNullOrEmpty(writer.ToString()));
                return data;
            }
        }

        [Fact]
        public void ReadConfigFile_EmptySignList_ReturnsEmptyFileNames()
        {
            var json = @"{ ""sign"": [] }";
            var data = LoadConfigFile(json);
            Assert.Empty(data.FileNames);
        }

        [Fact]
        public void ReadConfigFile_MissingExcludeSection_ReturnsEmptyExternalFileNames()
        {
            var json = @"{ ""sign"": [] }";
            var data = LoadConfigFile(json);
            Assert.Empty(data.ExternalFileNames);
        }

        [Fact]
        public void ReadConfigFile_SingleEntry_ParsesCertificate()
        {
            var json = @"
{
  ""sign"": [
    {
      ""certificate"": ""MyCertificate"",
      ""strongName"": null,
      ""values"": [ ""file.dll"" ]
    }
  ]
}";
            var data = LoadConfigFile(json);
            var signInfo = data.FileSignInfoMap.Values.Single();
            Assert.Equal("MyCertificate", signInfo.Certificate);
        }

        [Fact]
        public void ReadConfigFile_SingleEntry_ParsesStrongName()
        {
            var json = @"
{
  ""sign"": [
    {
      ""certificate"": ""MyCertificate"",
      ""strongName"": ""MyStrongName"",
      ""values"": [ ""file.dll"" ]
    }
  ]
}";
            var data = LoadConfigFile(json);
            var signInfo = data.FileSignInfoMap.Values.Single();
            Assert.Equal("MyStrongName", signInfo.StrongName);
        }

        [Fact]
        public void ReadConfigFile_MultipleFilesInOneEntry_AllFilesPresent()
        {
            var json = @"
{
  ""sign"": [
    {
      ""certificate"": ""MyCertificate"",
      ""strongName"": null,
      ""values"": [ ""a.dll"", ""b.dll"", ""c.dll"" ]
    }
  ]
}";
            var data = LoadConfigFile(json);
            var names = data.FileNames.Select(x => x.Name).OrderBy(x => x).ToArray();
            Assert.Equal(new[] { "a.dll", "b.dll", "c.dll" }, names);
        }

        [Fact]
        public void ReadConfigFile_MultipleSignEntries_AllFilesPresent()
        {
            var json = @"
{
  ""sign"": [
    {
      ""certificate"": ""Cert1"",
      ""strongName"": null,
      ""values"": [ ""first.dll"" ]
    },
    {
      ""certificate"": ""Cert2"",
      ""strongName"": null,
      ""values"": [ ""second.msi"" ]
    }
  ]
}";
            var data = LoadConfigFile(json);
            Assert.Equal(2, data.FileNames.Count());
        }

        [Fact]
        public void ReadConfigFile_MultipleSignEntries_CertificatesMappedCorrectly()
        {
            var json = @"
{
  ""sign"": [
    {
      ""certificate"": ""Cert1"",
      ""strongName"": null,
      ""values"": [ ""first.dll"" ]
    },
    {
      ""certificate"": ""Cert2"",
      ""strongName"": null,
      ""values"": [ ""second.msi"" ]
    }
  ]
}";
            var data = LoadConfigFile(json);
            var firstEntry = data.FileNames.Single(x => x.Name == "first.dll");
            var secondEntry = data.FileNames.Single(x => x.Name == "second.msi");
            Assert.Equal("Cert1", data.FileSignInfoMap[firstEntry].Certificate);
            Assert.Equal("Cert2", data.FileSignInfoMap[secondEntry].Certificate);
        }

        [Fact]
        public void ReadConfigFile_WithExcludeList_ParsedCorrectly()
        {
            var json = @"
{
  ""sign"": [],
  ""exclude"": [ ""excluded.dll"", ""also-excluded.dll"" ]
}";
            var data = LoadConfigFile(json);
            Assert.Contains("excluded.dll", data.ExternalFileNames);
            Assert.Contains("also-excluded.dll", data.ExternalFileNames);
        }

        [Fact]
        public void ReadConfigFile_WithPublishUrl_ParsedCorrectly()
        {
            var json = @"
{
  ""sign"": [
    {
      ""certificate"": ""MyCert"",
      ""strongName"": null,
      ""values"": [ ""file.dll"" ]
    }
  ],
  ""publishUrl"": ""https://example.com/feed""
}";
            var data = LoadConfigFile(json);
            Assert.Equal("https://example.com/feed", data.PublishUri);
        }

        [Fact]
        public void ReadConfigFile_MissingPublishUrl_DefaultsToUnset()
        {
            var json = @"{ ""sign"": [] }";
            var data = LoadConfigFile(json);
            Assert.Equal("unset", data.PublishUri);
        }

        [Fact]
        public void ReadConfigFile_MsiFile_ParsedCorrectly()
        {
            var json = @"
{
  ""sign"": [
    {
      ""certificate"": ""Microsoft402"",
      ""strongName"": null,
      ""values"": [ ""installer.msi"" ]
    }
  ]
}";
            var data = LoadConfigFile(json);
            Assert.Equal(new[] { "installer.msi" }, data.FileNames.Select(x => x.Name));
        }

        [Fact]
        public void ReadConfigFile_NupkgFile_ParsedCorrectly()
        {
            var json = @"
{
  ""sign"": [
    {
      ""certificate"": null,
      ""strongName"": null,
      ""values"": [ ""package.nupkg"" ]
    }
  ]
}";
            var data = LoadConfigFile(json);
            Assert.Equal(new[] { "package.nupkg" }, data.FileNames.Select(x => x.Name));
        }

        #endregion

        #region TryReadOrchestrationConfigFile

        private static BatchSignInput LoadOrchestrationConfigFile(string outputPath, string json)
        {
            using (var reader = new StringReader(json))
            using (var writer = new StringWriter())
            {
                Assert.True(Program.TryReadOrchestrationConfigFile(writer, reader, outputPath, out var data));
                Assert.True(string.IsNullOrEmpty(writer.ToString()));
                return data;
            }
        }

        private static string CreateTempFile(string directory, string relativePath)
        {
            var fullPath = Path.Combine(directory, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            File.WriteAllBytes(fullPath, Array.Empty<byte>());
            return fullPath;
        }

        [Fact]
        public void ReadOrchestrationConfigFile_SingleEntry_ParsesCertificate()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            try
            {
                CreateTempFile(tempDir, "file.dll");
                var json = $@"
{{
  ""sign"": [
    {{
      ""certificate"": ""OrchestratedCert"",
      ""strongName"": null,
      ""values"": [
        {{
          ""filePath"": ""file.dll"",
          ""sha256Hash"": ""AABBCCDD"",
          ""publishtofeedurl"": ""https://feeds.example.com""
        }}
      ]
    }}
  ]
}}";
                var data = LoadOrchestrationConfigFile(tempDir, json);
                var signInfo = data.FileSignInfoMap.Values.Single();
                Assert.Equal("OrchestratedCert", signInfo.Certificate);
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, recursive: true);
            }
        }

        [Fact]
        public void ReadOrchestrationConfigFile_SingleEntry_ParsesFilePath()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            try
            {
                CreateTempFile(tempDir, "file.dll");
                var json = @"
{
  ""sign"": [
    {
      ""certificate"": ""OrchestratedCert"",
      ""strongName"": null,
      ""values"": [
        {
          ""filePath"": ""file.dll"",
          ""sha256Hash"": ""AABBCCDD"",
          ""publishtofeedurl"": ""https://feeds.example.com""
        }
      ]
    }
  ]
}";
                var data = LoadOrchestrationConfigFile(tempDir, json);
                var entry = data.FileSignInfoMap.Keys.Single();
                Assert.Equal("file.dll", entry.RelativePath);
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, recursive: true);
            }
        }

        [Fact]
        public void ReadOrchestrationConfigFile_SingleEntry_ParsesSHA256Hash()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            try
            {
                CreateTempFile(tempDir, "file.dll");
                var json = @"
{
  ""sign"": [
    {
      ""certificate"": ""OrchestratedCert"",
      ""strongName"": null,
      ""values"": [
        {
          ""filePath"": ""file.dll"",
          ""sha256Hash"": ""AABBCCDDEEFF0011"",
          ""publishtofeedurl"": ""https://feeds.example.com""
        }
      ]
    }
  ]
}";
                var data = LoadOrchestrationConfigFile(tempDir, json);
                var entry = data.FileSignInfoMap.Keys.Single();
                Assert.Equal("AABBCCDDEEFF0011", entry.SHA256Hash);
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, recursive: true);
            }
        }

        [Fact]
        public void ReadOrchestrationConfigFile_SingleEntry_ParsesPublishUrl()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            try
            {
                CreateTempFile(tempDir, "file.dll");
                var json = @"
{
  ""sign"": [
    {
      ""certificate"": ""OrchestratedCert"",
      ""strongName"": null,
      ""values"": [
        {
          ""filePath"": ""file.dll"",
          ""sha256Hash"": ""AABBCCDD"",
          ""publishtofeedurl"": ""https://feeds.example.com/package""
        }
      ]
    }
  ]
}";
                var data = LoadOrchestrationConfigFile(tempDir, json);
                Assert.Equal("https://feeds.example.com/package", data.PublishUri);
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, recursive: true);
            }
        }

        [Fact]
        public void ReadOrchestrationConfigFile_WithExcludeList_ParsedCorrectly()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            try
            {
                CreateTempFile(tempDir, "file.dll");
                var json = @"
{
  ""sign"": [
    {
      ""certificate"": ""OrchestratedCert"",
      ""strongName"": null,
      ""values"": [
        {
          ""filePath"": ""file.dll"",
          ""sha256Hash"": ""AABBCCDD"",
          ""publishtofeedurl"": ""https://feeds.example.com""
        }
      ]
    }
  ],
  ""exclude"": [ ""external.dll"" ]
}";
                var data = LoadOrchestrationConfigFile(tempDir, json);
                Assert.Contains("external.dll", data.ExternalFileNames);
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, recursive: true);
            }
        }

        [Fact]
        public void ReadOrchestrationConfigFile_MultipleEntries_AllEntriesPresent()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            try
            {
                CreateTempFile(tempDir, "first.dll");
                CreateTempFile(tempDir, "second.dll");
                var json = @"
{
  ""sign"": [
    {
      ""certificate"": ""Cert1"",
      ""strongName"": null,
      ""values"": [
        {
          ""filePath"": ""first.dll"",
          ""sha256Hash"": ""AABB"",
          ""publishtofeedurl"": ""https://feeds.example.com""
        }
      ]
    },
    {
      ""certificate"": ""Cert2"",
      ""strongName"": ""MyStrongName"",
      ""values"": [
        {
          ""filePath"": ""second.dll"",
          ""sha256Hash"": ""CCDD"",
          ""publishtofeedurl"": ""https://feeds.example.com""
        }
      ]
    }
  ]
}";
                var data = LoadOrchestrationConfigFile(tempDir, json);
                Assert.Equal(2, data.FileSignInfoMap.Count);
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, recursive: true);
            }
        }

        [Fact]
        public void ReadOrchestrationConfigFile_WithStrongName_ParsedCorrectly()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            try
            {
                CreateTempFile(tempDir, "file.dll");
                var json = @"
{
  ""sign"": [
    {
      ""certificate"": ""MyCert"",
      ""strongName"": ""MyStrongName"",
      ""values"": [
        {
          ""filePath"": ""file.dll"",
          ""sha256Hash"": ""AABBCCDD"",
          ""publishtofeedurl"": ""https://feeds.example.com""
        }
      ]
    }
  ]
}";
                var data = LoadOrchestrationConfigFile(tempDir, json);
                var signInfo = data.FileSignInfoMap.Values.Single();
                Assert.Equal("MyStrongName", signInfo.StrongName);
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, recursive: true);
            }
        }

        [Fact]
        public void ReadOrchestrationConfigFile_KindFieldIgnored_ParsesSuccessfully()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            try
            {
                CreateTempFile(tempDir, "file.dll");
                var json = @"
{
  ""kind"": ""orchestration"",
  ""sign"": [
    {
      ""certificate"": ""MyCert"",
      ""strongName"": null,
      ""values"": [
        {
          ""filePath"": ""file.dll"",
          ""sha256Hash"": ""AABBCCDD"",
          ""publishtofeedurl"": ""https://feeds.example.com""
        }
      ]
    }
  ]
}";
                var data = LoadOrchestrationConfigFile(tempDir, json);
                Assert.Single(data.FileSignInfoMap);
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, recursive: true);
            }
        }

        #endregion
    }
}

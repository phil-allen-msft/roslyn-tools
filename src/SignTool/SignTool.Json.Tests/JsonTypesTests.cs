// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the License.txt file in the project root for more information.

using System.Text.Json;
using Xunit;

namespace SignTool.Json.Tests
{
    /// <summary>
    /// Tests verifying that the JSON type classes deserialize correctly from JSON.
    /// </summary>
    public class JsonTypesTests
    {
        #region FileJson deserialization

        [Fact]
        public void FileJson_Deserialize_KindProperty()
        {
            var json = @"{ ""kind"": ""default"", ""sign"": [] }";
            var result = JsonSerializer.Deserialize<FileJson>(json);
            Assert.Equal("default", result.Kind);
        }

        [Fact]
        public void FileJson_Deserialize_PublishUrlProperty()
        {
            var json = @"{ ""publishUrl"": ""https://example.com/feed"", ""sign"": [] }";
            var result = JsonSerializer.Deserialize<FileJson>(json);
            Assert.Equal("https://example.com/feed", result.PublishUrl);
        }

        [Fact]
        public void FileJson_Deserialize_SignListWithSingleEntry()
        {
            var json = @"
{
  ""sign"": [
    {
      ""certificate"": ""MyCert"",
      ""strongName"": ""MySN"",
      ""values"": [ ""a.dll"", ""b.dll"" ]
    }
  ]
}";
            var result = JsonSerializer.Deserialize<FileJson>(json);
            Assert.Single(result.SignList);
            Assert.Equal("MyCert", result.SignList[0].Certificate);
            Assert.Equal("MySN", result.SignList[0].StrongName);
            Assert.Equal(new[] { "a.dll", "b.dll" }, result.SignList[0].FileList);
        }

        [Fact]
        public void FileJson_Deserialize_SignListWithMultipleEntries()
        {
            var json = @"
{
  ""sign"": [
    { ""certificate"": ""Cert1"", ""strongName"": null, ""values"": [ ""a.dll"" ] },
    { ""certificate"": ""Cert2"", ""strongName"": null, ""values"": [ ""b.msi"" ] }
  ]
}";
            var result = JsonSerializer.Deserialize<FileJson>(json);
            Assert.Equal(2, result.SignList.Length);
        }

        [Fact]
        public void FileJson_Deserialize_ExcludeList()
        {
            var json = @"
{
  ""sign"": [],
  ""exclude"": [ ""skip1.dll"", ""skip2.dll"" ]
}";
            var result = JsonSerializer.Deserialize<FileJson>(json);
            Assert.Equal(new[] { "skip1.dll", "skip2.dll" }, result.ExcludeList);
        }

        [Fact]
        public void FileJson_Deserialize_NullExcludeList_WhenMissing()
        {
            var json = @"{ ""sign"": [] }";
            var result = JsonSerializer.Deserialize<FileJson>(json);
            Assert.Null(result.ExcludeList);
        }

        [Fact]
        public void FileJson_Deserialize_NullPublishUrl_WhenMissing()
        {
            var json = @"{ ""sign"": [] }";
            var result = JsonSerializer.Deserialize<FileJson>(json);
            Assert.Null(result.PublishUrl);
        }

        #endregion

        #region FileSignData deserialization

        [Fact]
        public void FileSignData_Deserialize_CertificateProperty()
        {
            var json = @"{ ""certificate"": ""MyCertificate"", ""strongName"": null, ""values"": [] }";
            var result = JsonSerializer.Deserialize<FileSignData>(json);
            Assert.Equal("MyCertificate", result.Certificate);
        }

        [Fact]
        public void FileSignData_Deserialize_StrongNameProperty()
        {
            var json = @"{ ""certificate"": ""MyCert"", ""strongName"": ""MyStrongName"", ""values"": [] }";
            var result = JsonSerializer.Deserialize<FileSignData>(json);
            Assert.Equal("MyStrongName", result.StrongName);
        }

        [Fact]
        public void FileSignData_Deserialize_NullStrongName()
        {
            var json = @"{ ""certificate"": ""MyCert"", ""strongName"": null, ""values"": [] }";
            var result = JsonSerializer.Deserialize<FileSignData>(json);
            Assert.Null(result.StrongName);
        }

        [Fact]
        public void FileSignData_Deserialize_ValuesAsFileList()
        {
            var json = @"{ ""certificate"": ""Cert"", ""strongName"": null, ""values"": [ ""one.dll"", ""two.dll"", ""three.dll"" ] }";
            var result = JsonSerializer.Deserialize<FileSignData>(json);
            Assert.Equal(new[] { "one.dll", "two.dll", "three.dll" }, result.FileList);
        }

        #endregion

        #region OrchestratedFileJson deserialization

        [Fact]
        public void OrchestratedFileJson_Deserialize_KindProperty()
        {
            var json = @"{ ""kind"": ""orchestration"", ""sign"": [] }";
            var result = JsonSerializer.Deserialize<OrchestratedFileJson>(json);
            Assert.Equal("orchestration", result.Kind);
        }

        [Fact]
        public void OrchestratedFileJson_Deserialize_SignListWithFileEntries()
        {
            var json = @"
{
  ""sign"": [
    {
      ""certificate"": ""MyCert"",
      ""strongName"": null,
      ""values"": [
        {
          ""filePath"": ""path/to/file.dll"",
          ""sha256Hash"": ""AABBCCDD"",
          ""publishtofeedurl"": ""https://feeds.example.com""
        }
      ]
    }
  ]
}";
            var result = JsonSerializer.Deserialize<OrchestratedFileJson>(json);
            Assert.Single(result.SignList);
            Assert.Equal("MyCert", result.SignList[0].Certificate);
            Assert.Single(result.SignList[0].FileList);
        }

        [Fact]
        public void OrchestratedFileJson_Deserialize_ExcludeList()
        {
            var json = @"
{
  ""sign"": [],
  ""exclude"": [ ""external1.dll"", ""external2.dll"" ]
}";
            var result = JsonSerializer.Deserialize<OrchestratedFileJson>(json);
            Assert.Equal(new[] { "external1.dll", "external2.dll" }, result.ExcludeList);
        }

        #endregion

        #region OrchestratedFileSignData deserialization

        [Fact]
        public void OrchestratedFileSignData_Deserialize_CertificateAndStrongName()
        {
            var json = @"
{
  ""certificate"": ""OrchestratedCert"",
  ""strongName"": ""OrchestratedSN"",
  ""values"": []
}";
            var result = JsonSerializer.Deserialize<OrchestratedFileSignData>(json);
            Assert.Equal("OrchestratedCert", result.Certificate);
            Assert.Equal("OrchestratedSN", result.StrongName);
        }

        [Fact]
        public void OrchestratedFileSignData_Deserialize_FileListEntries()
        {
            var json = @"
{
  ""certificate"": ""Cert"",
  ""strongName"": null,
  ""values"": [
    {
      ""filePath"": ""dir/file1.dll"",
      ""sha256Hash"": ""HASH1"",
      ""publishtofeedurl"": ""https://feed1.example.com""
    },
    {
      ""filePath"": ""dir/file2.dll"",
      ""sha256Hash"": ""HASH2"",
      ""publishtofeedurl"": ""https://feed2.example.com""
    }
  ]
}";
            var result = JsonSerializer.Deserialize<OrchestratedFileSignData>(json);
            Assert.Equal(2, result.FileList.Length);
        }

        #endregion

        #region FileSignDataEntry deserialization

        [Fact]
        public void FileSignDataEntry_Deserialize_FilePathProperty()
        {
            var json = @"{ ""filePath"": ""relative/path/file.dll"", ""sha256Hash"": ""AABB"", ""publishtofeedurl"": ""https://example.com"" }";
            var result = JsonSerializer.Deserialize<FileSignDataEntry>(json);
            Assert.Equal("relative/path/file.dll", result.FilePath);
        }

        [Fact]
        public void FileSignDataEntry_Deserialize_SHA256HashProperty()
        {
            var json = @"{ ""filePath"": ""file.dll"", ""sha256Hash"": ""DEADBEEFCAFE0123"", ""publishtofeedurl"": ""https://example.com"" }";
            var result = JsonSerializer.Deserialize<FileSignDataEntry>(json);
            Assert.Equal("DEADBEEFCAFE0123", result.SHA256Hash);
        }

        [Fact]
        public void FileSignDataEntry_Deserialize_PublishToFeedUrlProperty()
        {
            var json = @"{ ""filePath"": ""file.dll"", ""sha256Hash"": ""AABB"", ""publishtofeedurl"": ""https://myfeed.example.com/package"" }";
            var result = JsonSerializer.Deserialize<FileSignDataEntry>(json);
            Assert.Equal("https://myfeed.example.com/package", result.PublishToFeedUrl);
        }

        [Fact]
        public void FileSignDataEntry_Deserialize_NullSHA256Hash_WhenMissing()
        {
            var json = @"{ ""filePath"": ""file.dll"", ""publishtofeedurl"": ""https://example.com"" }";
            var result = JsonSerializer.Deserialize<FileSignDataEntry>(json);
            Assert.Null(result.SHA256Hash);
        }

        #endregion
    }
}

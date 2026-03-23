// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the License.txt file in the project root for more information.

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace SignTool.Json.Tests
{
    /// <summary>
    /// Tests verifying that the JSON type classes serialize correctly to JSON and that
    /// the orchestration manifest format is preserved through a round-trip.
    /// </summary>
    public class JsonWritingTests
    {
        #region OrchestratedFileJson serialization

        [Fact]
        public void OrchestratedFileJson_Serialize_ProducesValidJson()
        {
            var obj = new OrchestratedFileJson
            {
                Kind = "orchestration",
                SignList = new[]
                {
                    new OrchestratedFileSignData
                    {
                        Certificate = "MyCert",
                        StrongName = null,
                        FileList = new[]
                        {
                            new FileSignDataEntry
                            {
                                FilePath = "file.dll",
                                SHA256Hash = "AABBCCDD",
                                PublishToFeedUrl = "https://feeds.example.com"
                            }
                        }
                    }
                },
                ExcludeList = new[] { "excluded.dll" }
            };

            var json = JsonConvert.SerializeObject(obj, Formatting.Indented);
            Assert.NotNull(json);
            Assert.NotEmpty(json);

            // Verify it parses back as valid JSON
            var parsed = JObject.Parse(json);
            Assert.NotNull(parsed);
        }

        [Fact]
        public void OrchestratedFileJson_Serialize_KindPropertyName()
        {
            var obj = new OrchestratedFileJson { Kind = "orchestration", SignList = new OrchestratedFileSignData[0] };
            var json = JsonConvert.SerializeObject(obj);
            var parsed = JObject.Parse(json);
            Assert.Equal("orchestration", parsed["kind"]?.Value<string>());
        }

        [Fact]
        public void OrchestratedFileJson_Serialize_SignPropertyName()
        {
            var obj = new OrchestratedFileJson
            {
                SignList = new[]
                {
                    new OrchestratedFileSignData
                    {
                        Certificate = "Cert",
                        FileList = new FileSignDataEntry[0]
                    }
                }
            };
            var json = JsonConvert.SerializeObject(obj);
            var parsed = JObject.Parse(json);
            Assert.NotNull(parsed["sign"]);
            Assert.True(parsed["sign"] is JArray);
        }

        [Fact]
        public void OrchestratedFileJson_Serialize_ExcludePropertyName()
        {
            var obj = new OrchestratedFileJson
            {
                SignList = new OrchestratedFileSignData[0],
                ExcludeList = new[] { "ext.dll" }
            };
            var json = JsonConvert.SerializeObject(obj);
            var parsed = JObject.Parse(json);
            Assert.NotNull(parsed["exclude"]);
        }

        [Fact]
        public void OrchestratedFileSignData_Serialize_CertificatePropertyName()
        {
            var obj = new OrchestratedFileSignData { Certificate = "TestCert", FileList = new FileSignDataEntry[0] };
            var json = JsonConvert.SerializeObject(obj);
            var parsed = JObject.Parse(json);
            Assert.Equal("TestCert", parsed["certificate"]?.Value<string>());
        }

        [Fact]
        public void OrchestratedFileSignData_Serialize_StrongNamePropertyName()
        {
            var obj = new OrchestratedFileSignData { StrongName = "MySN", FileList = new FileSignDataEntry[0] };
            var json = JsonConvert.SerializeObject(obj);
            var parsed = JObject.Parse(json);
            Assert.Equal("MySN", parsed["strongName"]?.Value<string>());
        }

        [Fact]
        public void OrchestratedFileSignData_Serialize_ValuesPropertyName()
        {
            var obj = new OrchestratedFileSignData { FileList = new FileSignDataEntry[0] };
            var json = JsonConvert.SerializeObject(obj);
            var parsed = JObject.Parse(json);
            Assert.NotNull(parsed["values"]);
        }

        [Fact]
        public void FileSignDataEntry_Serialize_FilePathPropertyName()
        {
            var entry = new FileSignDataEntry { FilePath = "path/to/file.dll", SHA256Hash = "HASH", PublishToFeedUrl = "https://feed.example.com" };
            var json = JsonConvert.SerializeObject(entry);
            var parsed = JObject.Parse(json);
            Assert.Equal("path/to/file.dll", parsed["filePath"]?.Value<string>());
        }

        [Fact]
        public void FileSignDataEntry_Serialize_SHA256HashPropertyName()
        {
            var entry = new FileSignDataEntry { FilePath = "file.dll", SHA256Hash = "DEADBEEF", PublishToFeedUrl = "https://feed.example.com" };
            var json = JsonConvert.SerializeObject(entry);
            var parsed = JObject.Parse(json);
            Assert.Equal("DEADBEEF", parsed["sha256Hash"]?.Value<string>());
        }

        [Fact]
        public void FileSignDataEntry_Serialize_PublishToFeedUrlPropertyName()
        {
            var entry = new FileSignDataEntry { FilePath = "file.dll", SHA256Hash = "HASH", PublishToFeedUrl = "https://myfeed.com" };
            var json = JsonConvert.SerializeObject(entry);
            var parsed = JObject.Parse(json);
            Assert.Equal("https://myfeed.com", parsed["publishtofeedurl"]?.Value<string>());
        }

        #endregion

        #region Round-trip tests

        [Fact]
        public void OrchestratedFileJson_RoundTrip_KindPreserved()
        {
            var original = new OrchestratedFileJson
            {
                Kind = "orchestration",
                SignList = new OrchestratedFileSignData[0]
            };
            var json = JsonConvert.SerializeObject(original, Formatting.Indented);
            var deserialized = JsonConvert.DeserializeObject<OrchestratedFileJson>(json);
            Assert.Equal(original.Kind, deserialized.Kind);
        }

        [Fact]
        public void OrchestratedFileJson_RoundTrip_SignListPreserved()
        {
            var original = new OrchestratedFileJson
            {
                SignList = new[]
                {
                    new OrchestratedFileSignData
                    {
                        Certificate = "RoundTripCert",
                        StrongName = "RoundTripSN",
                        FileList = new[]
                        {
                            new FileSignDataEntry
                            {
                                FilePath = "round/trip/file.dll",
                                SHA256Hash = "RTRT1234",
                                PublishToFeedUrl = "https://rt.example.com"
                            }
                        }
                    }
                }
            };
            var json = JsonConvert.SerializeObject(original, Formatting.Indented);
            var deserialized = JsonConvert.DeserializeObject<OrchestratedFileJson>(json);

            Assert.Single(deserialized.SignList);
            Assert.Equal("RoundTripCert", deserialized.SignList[0].Certificate);
            Assert.Equal("RoundTripSN", deserialized.SignList[0].StrongName);
            Assert.Single(deserialized.SignList[0].FileList);
            Assert.Equal("round/trip/file.dll", deserialized.SignList[0].FileList[0].FilePath);
            Assert.Equal("RTRT1234", deserialized.SignList[0].FileList[0].SHA256Hash);
            Assert.Equal("https://rt.example.com", deserialized.SignList[0].FileList[0].PublishToFeedUrl);
        }

        [Fact]
        public void OrchestratedFileJson_RoundTrip_ExcludeListPreserved()
        {
            var original = new OrchestratedFileJson
            {
                SignList = new OrchestratedFileSignData[0],
                ExcludeList = new[] { "excluded1.dll", "excluded2.dll" }
            };
            var json = JsonConvert.SerializeObject(original, Formatting.Indented);
            var deserialized = JsonConvert.DeserializeObject<OrchestratedFileJson>(json);
            Assert.Equal(original.ExcludeList, deserialized.ExcludeList);
        }

        [Fact]
        public void FileJson_RoundTrip_AllPropertiesPreserved()
        {
            var original = new FileJson
            {
                Kind = "default",
                PublishUrl = "https://example.com/publish",
                SignList = new[]
                {
                    new FileSignData
                    {
                        Certificate = "FileCert",
                        StrongName = "FileSN",
                        FileList = new[] { "file1.dll", "file2.dll" }
                    }
                },
                ExcludeList = new[] { "skip.dll" }
            };
            var json = JsonConvert.SerializeObject(original, Formatting.Indented);
            var deserialized = JsonConvert.DeserializeObject<FileJson>(json);

            Assert.Equal(original.Kind, deserialized.Kind);
            Assert.Equal(original.PublishUrl, deserialized.PublishUrl);
            Assert.Single(deserialized.SignList);
            Assert.Equal("FileCert", deserialized.SignList[0].Certificate);
            Assert.Equal("FileSN", deserialized.SignList[0].StrongName);
            Assert.Equal(new[] { "file1.dll", "file2.dll" }, deserialized.SignList[0].FileList);
            Assert.Equal(original.ExcludeList, deserialized.ExcludeList);
        }

        #endregion
    }
}

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the License.txt file in the project root for more information.

using System.Text.Json;
using roslyn.optprof.json;
using roslyn.optprof.lib;
using Xunit;

namespace roslyn.optprof.lib.tests
{
    public class JsonSerializationTests
    {
        private static readonly JsonSerializerOptions s_options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        #region Deserialization Tests

        [Fact]
        public void Deserialize_ProductsOnly_ReturnsCorrectConfiguration()
        {
            const string json = @"
{
  ""products"": [
    {
      ""name"": ""Roslyn.VisualStudio.Setup.vsix"",
      ""tests"": [
        {
          ""container"": ""DDRIT.RPS.CSharp"",
          ""testCases"": [
            ""DDRIT.RPS.CSharp.CSharpTest.EditingAndDesigner""
          ]
        }
      ]
    }
  ]
}";
            var config = Config.ReadConfigFile(json);

            Assert.NotNull(config);
            Assert.NotNull(config.Products);
            Assert.Single(config.Products);
            Assert.Equal("Roslyn.VisualStudio.Setup.vsix", config.Products[0].Name);
            Assert.Single(config.Products[0].Tests);
            Assert.Equal("DDRIT.RPS.CSharp", config.Products[0].Tests[0].Container);
            Assert.Single(config.Products[0].Tests[0].TestCases);
            Assert.Equal("DDRIT.RPS.CSharp.CSharpTest.EditingAndDesigner", config.Products[0].Tests[0].TestCases[0]);
            Assert.Null(config.Assemblies);
        }

        [Fact]
        public void Deserialize_AssembliesOnly_ReturnsCorrectConfiguration()
        {
            const string json = @"
{
  ""assemblies"": [
    {
      ""assembly"": ""System.Collections.Immutable.dll"",
      ""instrumentationArguments"": [
        {
          ""relativeInstallationFolder"": ""Common7/IDE/PrivateAssemblies"",
          ""instrumentationExecutable"": ""Common7/IDE/vsn.exe""
        }
      ],
      ""tests"": [
        {
          ""container"": ""DDRIT.RPS.CSharp"",
          ""testCases"": [
            ""DDRIT.RPS.CSharp.CSharpTest.BuildAndDebugging""
          ]
        }
      ]
    }
  ]
}";
            var config = Config.ReadConfigFile(json);

            Assert.NotNull(config);
            Assert.Null(config.Products);
            Assert.NotNull(config.Assemblies);
            Assert.Single(config.Assemblies);
            Assert.Equal("System.Collections.Immutable.dll", config.Assemblies[0].Assembly);
            Assert.Single(config.Assemblies[0].InstrumentationArguments);
            Assert.Equal("Common7/IDE/PrivateAssemblies", config.Assemblies[0].InstrumentationArguments[0].RelativeInstallationFolder);
            Assert.Equal("Common7/IDE/vsn.exe", config.Assemblies[0].InstrumentationArguments[0].InstrumentationExecutable);
            Assert.Single(config.Assemblies[0].Tests);
            Assert.Equal("DDRIT.RPS.CSharp", config.Assemblies[0].Tests[0].Container);
            Assert.Single(config.Assemblies[0].Tests[0].TestCases);
            Assert.Equal("DDRIT.RPS.CSharp.CSharpTest.BuildAndDebugging", config.Assemblies[0].Tests[0].TestCases[0]);
        }

        [Fact]
        public void Deserialize_ProductsAndAssemblies_ReturnsBothCollections()
        {
            const string json = @"
{
  ""products"": [
    {
      ""name"": ""Roslyn.VisualStudio.Setup.vsix"",
      ""tests"": [
        {
          ""container"": ""DDRIT.RPS.CSharp"",
          ""testCases"": [""DDRIT.RPS.CSharp.CSharpTest.EditingAndDesigner""]
        }
      ]
    }
  ],
  ""assemblies"": [
    {
      ""assembly"": ""System.Reflection.Metadata.dll"",
      ""instrumentationArguments"": [
        {
          ""relativeInstallationFolder"": ""Common7/IDE/PrivateAssemblies"",
          ""instrumentationExecutable"": ""Common7/IDE/vsn.exe""
        }
      ],
      ""tests"": [
        {
          ""container"": ""DDRIT.RPS.CSharp"",
          ""testCases"": [""DDRIT.RPS.CSharp.CSharpTest.BuildAndDebugging""]
        }
      ]
    }
  ]
}";
            var config = Config.ReadConfigFile(json);

            Assert.NotNull(config);
            Assert.Single(config.Products);
            Assert.Equal("Roslyn.VisualStudio.Setup.vsix", config.Products[0].Name);
            Assert.Single(config.Assemblies);
            Assert.Equal("System.Reflection.Metadata.dll", config.Assemblies[0].Assembly);
        }

        [Fact]
        public void Deserialize_EmptyJson_ReturnsConfigurationWithNullCollections()
        {
            var config = Config.ReadConfigFile("{}");

            Assert.NotNull(config);
            Assert.Null(config.Products);
            Assert.Null(config.Assemblies);
        }

        [Fact]
        public void Deserialize_MultipleProducts_ReturnsAllProducts()
        {
            const string json = @"
{
  ""products"": [
    { ""name"": ""Product1.vsix"", ""tests"": [] },
    { ""name"": ""Product2.vsix"", ""tests"": [] },
    { ""name"": ""Product3.vsix"", ""tests"": [] }
  ]
}";
            var config = Config.ReadConfigFile(json);

            Assert.NotNull(config.Products);
            Assert.Equal(3, config.Products.Length);
            Assert.Equal("Product1.vsix", config.Products[0].Name);
            Assert.Equal("Product2.vsix", config.Products[1].Name);
            Assert.Equal("Product3.vsix", config.Products[2].Name);
        }

        [Fact]
        public void Deserialize_MultipleTestCases_ReturnsAllTestCases()
        {
            const string json = @"
{
  ""products"": [
    {
      ""name"": ""Roslyn.VisualStudio.Setup.vsix"",
      ""tests"": [
        {
          ""container"": ""VSPE"",
          ""testCases"": [
            ""VSPE.OptProfTests.vs_perf_test1"",
            ""VSPE.OptProfTests.vs_perf_test2"",
            ""VSPE.OptProfTests.vs_perf_test3""
          ]
        }
      ]
    }
  ]
}";
            var config = Config.ReadConfigFile(json);

            Assert.Equal(3, config.Products[0].Tests[0].TestCases.Length);
            Assert.Equal("VSPE.OptProfTests.vs_perf_test1", config.Products[0].Tests[0].TestCases[0]);
            Assert.Equal("VSPE.OptProfTests.vs_perf_test2", config.Products[0].Tests[0].TestCases[1]);
            Assert.Equal("VSPE.OptProfTests.vs_perf_test3", config.Products[0].Tests[0].TestCases[2]);
        }

        [Fact]
        public void Deserialize_MultipleInstrumentationArguments_ReturnsAllArguments()
        {
            const string json = @"
{
  ""assemblies"": [
    {
      ""assembly"": ""System.Collections.Immutable.dll"",
      ""instrumentationArguments"": [
        {
          ""relativeInstallationFolder"": ""Common7/IDE/PrivateAssemblies"",
          ""instrumentationExecutable"": ""Common7/IDE/vsn.exe""
        },
        {
          ""relativeInstallationFolder"": ""MSBuild/15.0/Bin/Roslyn"",
          ""instrumentationExecutable"": ""Common7/IDE/vsn.exe""
        }
      ],
      ""tests"": []
    }
  ]
}";
            var config = Config.ReadConfigFile(json);

            Assert.Equal(2, config.Assemblies[0].InstrumentationArguments.Length);
            Assert.Equal("Common7/IDE/PrivateAssemblies", config.Assemblies[0].InstrumentationArguments[0].RelativeInstallationFolder);
            Assert.Equal("MSBuild/15.0/Bin/Roslyn", config.Assemblies[0].InstrumentationArguments[1].RelativeInstallationFolder);
        }

        #endregion

        #region Serialization Tests

        [Fact]
        public void Serialize_OptProfTrainingConfiguration_UsesCorrectPropertyNames()
        {
            var config = new OptProfTrainingConfiguration
            {
                Products = new[] { new ProductOptProfTraining { Name = "MyProduct.vsix", Tests = new OptProfTrainingTest[0] } },
                Assemblies = new AssemblyOptProfTraining[0]
            };

            var json = JsonSerializer.Serialize(config, s_options);

            Assert.Contains(@"""products""", json);
            Assert.Contains(@"""assemblies""", json);
            Assert.Contains(@"""name""", json);
        }

        [Fact]
        public void Serialize_ProductOptProfTraining_UsesCorrectPropertyNames()
        {
            var product = new ProductOptProfTraining
            {
                Name = "Roslyn.VisualStudio.Setup.vsix",
                Tests = new[] { new OptProfTrainingTest { Container = "VSPE", TestCases = new[] { "VSPE.Test1" } } }
            };

            var json = JsonSerializer.Serialize(product, s_options);

            Assert.Contains(@"""name""", json);
            Assert.Contains(@"""tests""", json);
            Assert.Contains(@"""Roslyn.VisualStudio.Setup.vsix""", json);
        }

        [Fact]
        public void Serialize_AssemblyOptProfTraining_UsesCorrectPropertyNames()
        {
            var assembly = new AssemblyOptProfTraining
            {
                Assembly = "System.Collections.Immutable.dll",
                InstrumentationArguments = new[]
                {
                    new OptProfInstrumentationArgument
                    {
                        RelativeInstallationFolder = "Common7/IDE/PrivateAssemblies",
                        InstrumentationExecutable = "Common7/IDE/vsn.exe"
                    }
                },
                Tests = new OptProfTrainingTest[0]
            };

            var json = JsonSerializer.Serialize(assembly, s_options);

            Assert.Contains(@"""assembly""", json);
            Assert.Contains(@"""instrumentationArguments""", json);
            Assert.Contains(@"""tests""", json);
            Assert.Contains(@"""System.Collections.Immutable.dll""", json);
        }

        [Fact]
        public void Serialize_OptProfInstrumentationArgument_UsesCorrectPropertyNames()
        {
            var arg = new OptProfInstrumentationArgument
            {
                RelativeInstallationFolder = "Common7/IDE/PrivateAssemblies",
                InstrumentationExecutable = "Common7/IDE/vsn.exe"
            };

            var json = JsonSerializer.Serialize(arg, s_options);

            Assert.Contains(@"""relativeInstallationFolder""", json);
            Assert.Contains(@"""instrumentationExecutable""", json);
            Assert.Contains(@"""Common7/IDE/PrivateAssemblies""", json);
            Assert.Contains(@"""Common7/IDE/vsn.exe""", json);
        }

        [Fact]
        public void Serialize_OptProfTrainingTest_UsesCorrectPropertyNames()
        {
            var test = new OptProfTrainingTest
            {
                Container = "DDRIT.RPS.CSharp",
                TestCases = new[] { "DDRIT.RPS.CSharp.CSharpTest.EditingAndDesigner" }
            };

            var json = JsonSerializer.Serialize(test, s_options);

            Assert.Contains(@"""container""", json);
            Assert.Contains(@"""testCases""", json);
            Assert.Contains(@"""DDRIT.RPS.CSharp""", json);
        }

        #endregion

        #region Round-Trip Tests

        [Fact]
        public void RoundTrip_FullConfiguration_PreservesAllData()
        {
            var original = new OptProfTrainingConfiguration
            {
                Products = new[]
                {
                    new ProductOptProfTraining
                    {
                        Name = "Roslyn.VisualStudio.Setup.vsix",
                        Tests = new[]
                        {
                            new OptProfTrainingTest
                            {
                                Container = "DDRIT.RPS.CSharp",
                                TestCases = new[] { "DDRIT.RPS.CSharp.CSharpTest.EditingAndDesigner" }
                            }
                        }
                    }
                },
                Assemblies = new[]
                {
                    new AssemblyOptProfTraining
                    {
                        Assembly = "System.Collections.Immutable.dll",
                        InstrumentationArguments = new[]
                        {
                            new OptProfInstrumentationArgument
                            {
                                RelativeInstallationFolder = "Common7/IDE/PrivateAssemblies",
                                InstrumentationExecutable = "Common7/IDE/vsn.exe"
                            }
                        },
                        Tests = new[]
                        {
                            new OptProfTrainingTest
                            {
                                Container = "DDRIT.RPS.CSharp",
                                TestCases = new[] { "DDRIT.RPS.CSharp.CSharpTest.BuildAndDebugging" }
                            }
                        }
                    }
                }
            };

            var json = JsonSerializer.Serialize(original, s_options);
            var restored = Config.ReadConfigFile(json);

            Assert.NotNull(restored);
            Assert.Single(restored.Products);
            Assert.Equal("Roslyn.VisualStudio.Setup.vsix", restored.Products[0].Name);
            Assert.Single(restored.Products[0].Tests);
            Assert.Equal("DDRIT.RPS.CSharp", restored.Products[0].Tests[0].Container);
            Assert.Single(restored.Products[0].Tests[0].TestCases);
            Assert.Equal("DDRIT.RPS.CSharp.CSharpTest.EditingAndDesigner", restored.Products[0].Tests[0].TestCases[0]);

            Assert.Single(restored.Assemblies);
            Assert.Equal("System.Collections.Immutable.dll", restored.Assemblies[0].Assembly);
            Assert.Single(restored.Assemblies[0].InstrumentationArguments);
            Assert.Equal("Common7/IDE/PrivateAssemblies", restored.Assemblies[0].InstrumentationArguments[0].RelativeInstallationFolder);
            Assert.Equal("Common7/IDE/vsn.exe", restored.Assemblies[0].InstrumentationArguments[0].InstrumentationExecutable);
            Assert.Single(restored.Assemblies[0].Tests);
            Assert.Equal("DDRIT.RPS.CSharp", restored.Assemblies[0].Tests[0].Container);
            Assert.Single(restored.Assemblies[0].Tests[0].TestCases);
            Assert.Equal("DDRIT.RPS.CSharp.CSharpTest.BuildAndDebugging", restored.Assemblies[0].Tests[0].TestCases[0]);
        }

        [Fact]
        public void RoundTrip_SerializeAndDeserialize_ProducesConsistentResults()
        {
            const string json = @"
{
  ""products"": [
    {
      ""name"": ""Roslyn.VisualStudio.Setup.vsix"",
      ""tests"": [
        {
          ""container"": ""DDRIT.RPS.CSharp"",
          ""testCases"": [""DDRIT.RPS.CSharp.CSharpTest.EditingAndDesigner""]
        }
      ]
    }
  ]
}";
            var first = Config.ReadConfigFile(json);
            var serialized = JsonSerializer.Serialize(first, s_options);
            var second = Config.ReadConfigFile(serialized);

            Assert.Equal(first.Products[0].Name, second.Products[0].Name);
            Assert.Equal(first.Products[0].Tests[0].Container, second.Products[0].Tests[0].Container);
            Assert.Equal(first.Products[0].Tests[0].TestCases[0], second.Products[0].Tests[0].TestCases[0]);
        }

        #endregion
    }
}


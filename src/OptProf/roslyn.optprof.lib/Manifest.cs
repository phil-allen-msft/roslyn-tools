// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the License.txt file in the project root for more information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

namespace roslyn.optprof.lib
{
    public static class Manifest
    {
        public const string IBC = "IBC";
        public const string ARGS = "/ExeConfig:\"%VisualStudio.InstallationUnderTest.Path%\\Common7\\IDE\\vsn.exe\"";
        public const string ROOT = "%VisualStudio.InstallationUnderTest.Path%";

        public static IEnumerable<(string Technology, string RelativeInstallationPath, string InstrumentationArguments)> GetNgenEntriesFromJsonManifest(JsonObject json)
        {
            if (json["extensionDir"] != null)
            {
                var extensionDir = json["extensionDir"].GetValue<string>().Replace("[installdir]\\", string.Empty);
                return json["files"].AsArray()
                    .Where(file => IsNgened(file) && IsAssembly(file))
                    .Select(file =>
                    {
                        string Technology = IBC;
                        string RelativeInstallationPath = $"{extensionDir}\\{file["fileName"].GetValue<string>().Replace("/", string.Empty)}";
                        string InstrumentationArguments = ARGS;
                        return (Technology, RelativeInstallationPath, InstrumentationArguments);
                    });
            }
            else
            {
                return json["files"].AsArray()
                    .Where(file => IsNgened(file) && IsAssembly(file))
                    .Select(file =>
                    {
                        string Technology = IBC;
                        string RelativeInstallationPath = file["fileName"].GetValue<string>().Replace("/Contents/", string.Empty).Replace("/", "\\");
                        string InstrumentationArguments = file["ngenApplication"] != null
                            ? $"/ExeConfig:\"{ROOT}{file["ngenApplication"].GetValue<string>().Replace("[installDir]", string.Empty)}\""
                            : ARGS;
                        return (Technology, RelativeInstallationPath, InstrumentationArguments);
                    });
            }
        }

        private static bool IsNgened(JsonNode file)
            => file["ngen"] != null || file["ngenPriority"] != null || file["ngenArchitecture"] != null || file["ngenApplication"] != null;

        private static bool IsAssembly(JsonNode file)
        {
            if (file["fileName"] == null)
            {
                return false;
            }

            var fileName = file["fileName"].GetValue<string>();
            return fileName.EndsWith("dll") || fileName.EndsWith("exe");
        }
    }
}

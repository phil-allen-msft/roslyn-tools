// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the License.txt file in the project root for more information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace roslyn.optprof.lib
{
    public static class Manifest
    {
        public const string IBC = "IBC";
        public const string ARGS = "/ExeConfig:\"%VisualStudio.InstallationUnderTest.Path%\\Common7\\IDE\\vsn.exe\"";
        public const string ROOT = "%VisualStudio.InstallationUnderTest.Path%";

        public static IEnumerable<(string Technology, string RelativeInstallationPath, string InstrumentationArguments)> GetNgenEntriesFromJsonManifest(VsixManifest json)
        {
            if (json.ExtensionDir != null)
            {
                var extensionDir = json.ExtensionDir.Replace("[installdir]\\", string.Empty);
                return json.Files
                    .Where(file => IsNgened(file) && IsAssembly(file))
                    .Select(file =>
                    {
                        string Technology = IBC;
                        string RelativeInstallationPath = $"{extensionDir}\\{file.FileName.Replace("/", string.Empty)}";
                        string InstrumentationArguments = ARGS;
                        return (Technology, RelativeInstallationPath, InstrumentationArguments);
                    });
            }
            else
            {
                return json.Files
                    .Where(file => IsNgened(file) && IsAssembly(file))
                    .Select(file =>
                    {
                        string Technology = IBC;
                        string RelativeInstallationPath = file.FileName.Replace("/Contents/", string.Empty).Replace("/", "\\");
                        string InstrumentationArguments = file.NgenApplication != null
                            ? $"/ExeConfig:\"{ROOT}{file.NgenApplication.Replace("[installDir]", string.Empty)}\""
                            : ARGS;
                        return (Technology, RelativeInstallationPath, InstrumentationArguments);
                    });
            }
        }

        private static bool IsNgened(VsixManifestFile file)
            => file.Ngen != null || file.NgenPriority != null || file.NgenArchitecture != null || file.NgenApplication != null;

        private static bool IsAssembly(VsixManifestFile file)
        {
            if (file.FileName == null)
            {
                return false;
            }

            return file.FileName.EndsWith("dll") || file.FileName.EndsWith("exe");
        }
    }
}

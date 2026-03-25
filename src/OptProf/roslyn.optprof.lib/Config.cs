// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the License.txt file in the project root for more information.
using System.Text.Json;
using roslyn.optprof.json;

namespace roslyn.optprof.lib
{
    public static class Config
    {
        private static readonly JsonSerializerOptions s_options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        public static OptProfTrainingConfiguration ReadConfigFile(string configJson)
            => JsonSerializer.Deserialize<OptProfTrainingConfiguration>(configJson, s_options);
    }
}

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the License.txt file in the project root for more information.
using System.Text.Json.Serialization;

namespace roslyn.optprof.json
{
    public sealed class AssemblyOptProfTraining
    {
        [JsonPropertyName("assembly")]
        public string Assembly { get; set; }

        [JsonPropertyName("instrumentationArguments")]
        public OptProfInstrumentationArgument[] InstrumentationArguments { get; set; }

        [JsonPropertyName("tests")]
        public OptProfTrainingTest[] Tests { get; set; }
    }
}

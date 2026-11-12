#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

using RFiDGear.Bridge;

using Xunit;

namespace RFiDGear.Tests.ExternalControl
{
    /// <summary>
    /// The protocol of the external control bridge, as <c>RFiDGear.Bridge.Contracts</c> declares it. The approved snapshot
    /// (<c>BridgeContractsApiSurfaceTests</c>) says what the types are; these tests say what has to be true of them so that
    /// the host can build its capability list from the attributes, a client can read every message, and nothing is added to
    /// the protocol without a capability, a wire name and a golden form.
    /// </summary>
    public class BridgeContractsTests
    {
        private static readonly Assembly Bridge = typeof(IBridgeService).Assembly;

        public static IEnumerable<object[]> ContractMethods()
        {
            foreach (var method in typeof(IBridgeService).GetMethods().Concat(typeof(IBridgeClientEvents).GetMethods()))
            {
                yield return new object[] { method.DeclaringType!.Name + "." + method.Name };
            }
        }

        private static IEnumerable<MethodInfo> AllMethods() =>
            typeof(IBridgeService).GetMethods().Concat(typeof(IBridgeClientEvents).GetMethods());

        private static string CamelCase(string name) => char.ToLowerInvariant(name[0]) + name[1..];

        private static IReadOnlyList<string> ConstantsOf(Type type) =>
            type.GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(f => f.IsLiteral && f.FieldType == typeof(string))
                .Select(f => (string)f.GetRawConstantValue()!)
                .ToList();

        // ── Capabilities ──

        [Theory]
        [MemberData(nameof(ContractMethods))]
        public void EveryMethod_HasExactlyOneCapability_ThatTheProtocolKnows(string methodName)
        {
            var method = AllMethods().Single(m => m.DeclaringType!.Name + "." + m.Name == methodName);

            var attributes = method.GetCustomAttributes<CapabilityAttribute>().ToList();

            var capability = Assert.Single(attributes);
            Assert.Contains(capability.Name, BridgeCapabilities.All);
        }

        [Fact]
        public void EveryCapability_IsUsedByAMethod_SoNothingIsAdvertisedThatDoesNothing()
        {
            var used = AllMethods().Select(m => m.GetCustomAttribute<CapabilityAttribute>()!.Name).ToHashSet(StringComparer.Ordinal);

            Assert.Equal(BridgeCapabilities.All.OrderBy(n => n, StringComparer.Ordinal), used.OrderBy(n => n, StringComparer.Ordinal));
        }

        [Fact]
        public void CapabilityNames_AreDottedLowerCamelCase_AndEachIsListedOnce()
        {
            Assert.Equal(BridgeCapabilities.All.Count, BridgeCapabilities.All.Distinct(StringComparer.Ordinal).Count());
            Assert.Equal(BridgeCapabilities.All.OrderBy(n => n, StringComparer.Ordinal), ConstantsOf(typeof(BridgeCapabilities)).OrderBy(n => n, StringComparer.Ordinal));
            Assert.All(BridgeCapabilities.All, name => Assert.Matches("^[a-z][A-Za-z]*(\\.[a-z][A-Za-z]*)?$", name));
        }

        // ── Wire names ──

        [Fact]
        public void TheWireNamesOfTheMethods_AreTheCamelCaseOfTheMethodNames()
        {
            var fromTypes = typeof(IBridgeService).GetMethods().Select(m => CamelCase(m.Name)).OrderBy(n => n, StringComparer.Ordinal);

            Assert.Equal(fromTypes, ConstantsOf(typeof(BridgeMethods)).OrderBy(n => n, StringComparer.Ordinal));
        }

        [Fact]
        public void TheWireNamesOfTheEvents_AreTheCamelCaseOfTheMethodNames()
        {
            var fromTypes = typeof(IBridgeClientEvents).GetMethods().Select(m => CamelCase(m.Name)).OrderBy(n => n, StringComparer.Ordinal);

            Assert.Equal(fromTypes, BridgeEvents.All.OrderBy(n => n, StringComparer.Ordinal));
            Assert.Equal(BridgeEvents.All.OrderBy(n => n, StringComparer.Ordinal), ConstantsOf(typeof(BridgeEvents)).OrderBy(n => n, StringComparer.Ordinal));
        }

        // ── Shape ──

        [Fact]
        public void EveryServiceMethod_TakesOneRequestRecord_AndReturnsOneResponseRecord()
        {
            // One object in, one object out: a field can be added later without changing a signature.
            foreach (var method in typeof(IBridgeService).GetMethods())
            {
                var parameter = Assert.Single(method.GetParameters());
                Assert.True(parameter.ParameterType.Assembly == Bridge && parameter.ParameterType.IsSealed, $"{method.Name}: the parameter is not a sealed contract type");
                Assert.True(method.ReturnType.IsGenericType && method.ReturnType.GetGenericTypeDefinition() == typeof(Task<>), $"{method.Name}: the result is not a Task<T>");
                var response = method.ReturnType.GetGenericArguments()[0];
                Assert.True(response.Assembly == Bridge && response.IsSealed, $"{method.Name}: the response is not a sealed contract type");
                Assert.EndsWith("Request", parameter.ParameterType.Name, StringComparison.Ordinal);
                Assert.EndsWith("Response", response.Name, StringComparison.Ordinal);
            }
        }

        [Fact]
        public void EveryEventMethod_IsAVoidNotification_WithOneEvent()
        {
            // A notification gets no answer, and a client handler must not be able to hold up the host with one.
            foreach (var method in typeof(IBridgeClientEvents).GetMethods())
            {
                Assert.Equal(typeof(void), method.ReturnType);
                var parameter = Assert.Single(method.GetParameters());
                Assert.True(typeof(BridgeEvent).IsAssignableFrom(parameter.ParameterType), $"{method.Name}: the parameter is not an event");
                Assert.Equal(method.Name + "Event", parameter.ParameterType.Name);
            }
        }

        [Fact]
        public void EveryDataType_HasAnExplicitCamelCaseName_ForEveryProperty()
        {
            // The names on the wire must not depend on a naming policy of either side.
            var dataTypes = Bridge.GetExportedTypes().Where(IsDataType).ToList();
            Assert.NotEmpty(dataTypes);

            foreach (var type in dataTypes)
            {
                var names = new List<string>();
                foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    var attribute = property.GetCustomAttribute<JsonPropertyNameAttribute>();
                    Assert.True(attribute != null, $"{type.Name}.{property.Name} has no [JsonPropertyName]");
                    Assert.Matches("^[a-z][A-Za-z0-9]*$", attribute!.Name);
                    names.Add(attribute.Name);
                }

                Assert.Equal(names.Count, names.Distinct(StringComparer.Ordinal).Count());
            }
        }

        private static bool IsDataType(Type type) =>
            type.IsClass && !type.IsAbstract && !type.IsGenericType
            && (type.Name.EndsWith("Request", StringComparison.Ordinal) || type.Name.EndsWith("Response", StringComparison.Ordinal)
                || type.Name.EndsWith("Dto", StringComparison.Ordinal) || type.Name.EndsWith("Event", StringComparison.Ordinal)
                || type.Name == nameof(BridgeErrorData) || type.Name == nameof(VariablePair));

        // ── The golden form ──

        [Fact]
        public void EveryDataType_HasASample_SoNoMessageCanBeAddedWithoutItsWireForm()
        {
            var covered = BridgeSamples.All.Values.Select(v => v.GetType()).ToHashSet();
            var dataTypes = Bridge.GetExportedTypes().Where(IsDataType).ToList();

            // Types that only appear inside others are covered through them.
            var nested = new HashSet<Type> { typeof(VariablePair), typeof(CredentialDto), typeof(TaskOutcomeDto), typeof(ReaderDto), typeof(RunResultDto), typeof(ProjectStatusDto), typeof(ActiveRunDto), typeof(ProtocolVersionDto), typeof(BridgeLimitsDto) };

            Assert.All(dataTypes, type => Assert.True(covered.Contains(type) || nested.Contains(type), $"{type.Name} has no sample in BridgeSamples"));
            Assert.All(nested, type => Assert.Contains(type, dataTypes));
        }

        [Fact]
        public void EverySample_HasAGoldenForm_AndNothingElseIsThere()
        {
            Assert.Equal(BridgeSamples.All.Keys.OrderBy(k => k, StringComparer.Ordinal), BridgeGolden.Json.Keys.OrderBy(k => k, StringComparer.Ordinal));
        }

        public static IEnumerable<object[]> SampleNames() => BridgeSamples.All.Keys.Select(k => new object[] { k });

        [Theory]
        [MemberData(nameof(SampleNames))]
        public void ASample_IsWrittenAsItsGoldenForm(string name)
        {
            var sample = BridgeSamples.All[name];

            var json = JsonSerializer.Serialize(sample, sample.GetType(), BridgeJson.CreateClientOptions());

            Assert.Equal(BridgeGolden.Json[name], json);
        }

        [Theory]
        [MemberData(nameof(SampleNames))]
        public void TheGoldenForm_IsReadBackByBothEnds_ToTheSameValue(string name)
        {
            var type = BridgeSamples.All[name].GetType();
            var golden = BridgeGolden.Json[name];

            var byTheHost = JsonSerializer.Deserialize(golden, type, BridgeJson.CreateServerOptions());
            var byAClient = JsonSerializer.Deserialize(golden, type, BridgeJson.CreateClientOptions());

            // The records compare their lists by reference, so the comparison is made on what they write.
            Assert.Equal(golden, JsonSerializer.Serialize(byTheHost, type, BridgeJson.CreateClientOptions()));
            Assert.Equal(golden, JsonSerializer.Serialize(byAClient, type, BridgeJson.CreateClientOptions()));
        }

        // ── The serializer options ──

        [Fact]
        public void TheHost_RefusesAMemberItDoesNotKnow_AClientSkipsIt()
        {
            const string withExtra = """{"path":"C:\\a.rfPrj","somethingNewer":1}""";

            Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<LoadProjectRequest>(withExtra, BridgeJson.CreateServerOptions()));
            var read = JsonSerializer.Deserialize<LoadProjectRequest>(withExtra, BridgeJson.CreateClientOptions());
            Assert.Equal(@"C:\a.rfPrj", read!.Path);
        }

        [Fact]
        public void Names_AreMatchedExactly_NumbersAreNotReadFromStrings_AndNullsAreLeftOut()
        {
            var options = BridgeJson.CreateServerOptions();

            // "Path" instead of "path": with a tolerant reader the field would be silently ignored or matched, by accident.
            Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<LoadProjectRequest>("""{"Path":"x"}""", options));
            Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<LoadProjectResponse>("""{"path":"x","taskCount":"3"}""", options));

            var withoutOptionals = JsonSerializer.Serialize(new StartRunRequest { RequestId = "r-1" }, BridgeJson.CreateClientOptions());
            Assert.Equal("""{"requestId":"r-1","variables":[]}""", withoutOptionals);
        }

        [Fact]
        public void ADocumentThatIsTooDeep_IsRefused()
        {
            var deep = string.Concat(Enumerable.Repeat("""{"x":""", 40)) + "1" + new string('}', 40);

            Assert.ThrowsAny<JsonException>(() => JsonSerializer.Deserialize<JsonElement>(deep, BridgeJson.CreateServerOptions()));
        }

        // ── Errors, constants, helpers ──

        [Fact]
        public void EveryErrorCode_HasAKind_OfTheSameName_AndNoneIsInTheReservedRange()
        {
            var codes = typeof(BridgeErrorCodes).GetFields(BindingFlags.Public | BindingFlags.Static).Where(f => f.IsLiteral).ToDictionary(f => f.Name, f => (int)f.GetRawConstantValue()!);
            var kinds = typeof(BridgeErrorKinds).GetFields(BindingFlags.Public | BindingFlags.Static).Where(f => f.IsLiteral).ToDictionary(f => f.Name, f => (string)f.GetRawConstantValue()!);

            Assert.Equal(codes.Keys.OrderBy(k => k, StringComparer.Ordinal), kinds.Keys.OrderBy(k => k, StringComparer.Ordinal));
            Assert.Equal(codes.Count, codes.Values.Distinct().Count());
            Assert.Equal(kinds.Count, kinds.Values.Distinct(StringComparer.Ordinal).Count());
            Assert.All(codes.Values, code => Assert.InRange(code, 1001, 1999));
            Assert.All(kinds, pair => Assert.Equal(CamelCase(pair.Key), pair.Value));
        }

        [Theory]
        [InlineData(typeof(RunStatuses))]
        [InlineData(typeof(FailureReasons))]
        [InlineData(typeof(RunStates))]
        [InlineData(typeof(ReaderStates))]
        [InlineData(typeof(RunSources))]
        [InlineData(typeof(ShutdownReasons))]
        [InlineData(typeof(BridgeMethods))]
        [InlineData(typeof(BridgeEvents))]
        public void TheStringConstantsOfAClass_AreDistinct_AndWrittenInLowerCamelCase(Type type)
        {
            var values = ConstantsOf(type);

            Assert.NotEmpty(values);
            Assert.Equal(values.Count, values.Distinct(StringComparer.Ordinal).Count());
            Assert.All(values, value => Assert.Matches("^[a-z][A-Za-z]*$", value));
        }

        [Theory]
        [InlineData("r-1", true)]
        [InlineData("550e8400-e29b-41d4-a716-446655440000", true)]
        [InlineData("A.b_c:d-9", true)]
        [InlineData("", false)]
        [InlineData(null, false)]
        [InlineData("has space", false)]
        [InlineData("line\nbreak", false)]
        [InlineData("r-1\n", false)]
        [InlineData("r-1\r\n", false)]
        [InlineData("\nr-1", false)]
        [InlineData("slash/ok?", false)]
        public void ARequestId_IsAcceptedOnlyInTheSmallAlphabet(string? requestId, bool expected)
        {
            Assert.Equal(expected, BridgeProtocol.IsValidRequestId(requestId));
        }

        [Fact]
        public void ARequestId_IsAtMost64Characters()
        {
            Assert.True(BridgeProtocol.IsValidRequestId(new string('a', BridgeProtocol.MaxRequestIdLength)));
            Assert.False(BridgeProtocol.IsValidRequestId(new string('a', BridgeProtocol.MaxRequestIdLength + 1)));

            // A line break after the 64th character must not slip through either: "$" would allow it, "\z" does not.
            Assert.False(BridgeProtocol.IsValidRequestId(new string('a', BridgeProtocol.MaxRequestIdLength) + "\n"));
            Assert.Equal(64, BridgeProtocol.MaxRequestIdLength);
        }

        [Fact]
        public void ThePipeName_IsPerUser_AndNeedsAUser()
        {
            Assert.Equal("RFiDGear.Bridge.S-1-5-21-1-2-3-1001", BridgeProtocol.PipeNameFor("S-1-5-21-1-2-3-1001"));
            Assert.Equal("RFiDGear.Bridge.S-1-5-18", BridgeProtocol.PipeNameFor("  S-1-5-18 "));
            Assert.Throws<ArgumentException>(() => BridgeProtocol.PipeNameFor(" "));
            Assert.Throws<ArgumentException>(() => BridgeProtocol.PipeNameFor(string.Empty));
        }
    }
}

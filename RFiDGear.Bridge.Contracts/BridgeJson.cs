using System.Text.Json;
using System.Text.Json.Serialization;

namespace RFiDGear.Bridge
{
    /// <summary>
    /// The JSON options of both ends, in one place so that the host and a client read and write the same wire format.
    /// Property names are written out on every type (<see cref="JsonPropertyNameAttribute"/>), so they do not depend on a
    /// naming policy; nulls are left out; numbers are not read from strings; names are matched exactly.
    /// </summary>
    public static class BridgeJson
    {
        /// <summary>
        /// Creates the options the host reads requests with: a member that this version does not know is an error
        /// (<see cref="BridgeErrorCodes.InvalidRequest"/>), because a newer client's field would otherwise be ignored without a word.
        /// </summary>
        /// <returns>New options; they may be changed by the caller, for example to add a converter.</returns>
        public static JsonSerializerOptions CreateServerOptions() => Create(JsonUnmappedMemberHandling.Disallow);

        /// <summary>
        /// Creates the options a client reads responses and events with: a member that is not known is skipped, so a host
        /// that added an optional field with a newer minor version does not break an older client.
        /// </summary>
        /// <returns>New options; they may be changed by the caller.</returns>
        public static JsonSerializerOptions CreateClientOptions() => Create(JsonUnmappedMemberHandling.Skip);

        private static JsonSerializerOptions Create(JsonUnmappedMemberHandling unmapped) => new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = false,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            NumberHandling = JsonNumberHandling.Strict,
            UnmappedMemberHandling = unmapped,
            MaxDepth = 16,
            WriteIndented = false,
        };
    }
}

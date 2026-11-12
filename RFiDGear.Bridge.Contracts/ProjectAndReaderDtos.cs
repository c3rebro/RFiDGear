using System.Text.Json.Serialization;

namespace RFiDGear.Bridge
{
    /// <summary>The parameters of <c>loadProject</c>.</summary>
    public sealed record LoadProjectRequest
    {
        /// <summary>
        /// Gets the full path of the project file. It must be fully qualified and must not be a device path. Loading replaces
        /// the project in the main window; changes in it that were not saved are lost.
        /// </summary>
        [JsonPropertyName("path")]
        public string Path { get; init; } = string.Empty;
    }

    /// <summary>The answer to <c>loadProject</c>. A project that cannot be loaded is an error, not an answer.</summary>
    public sealed record LoadProjectResponse
    {
        /// <summary>Gets the full path of the project as the host knows it.</summary>
        [JsonPropertyName("path")]
        public string Path { get; init; } = string.Empty;

        /// <summary>Gets how many tasks the project has.</summary>
        [JsonPropertyName("taskCount")]
        public int TaskCount { get; init; }
    }

    /// <summary>The parameters of <c>scanReader</c>. There are none yet; the type exists so that an optional field can be added later.</summary>
    public sealed record ScanReaderRequest;

    /// <summary>The answer to <c>scanReader</c>. A reader that is busy or has no card is an answer, not an error.</summary>
    public sealed record ScanReaderResponse
    {
        /// <summary>Gets what the reader sees.</summary>
        [JsonPropertyName("reader")]
        public ReaderDto Reader { get; init; } = new ReaderDto();
    }
}

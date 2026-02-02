using System.Text.Json;
using System.Text.Json.Serialization;

namespace CodeClone.Domain;

/// <summary>
/// Source-generated JSON serialization context for AOT compatibility.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    WriteIndented = false,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
)]
[JsonSerializable(typeof(AnalyzeResponse))]
[JsonSerializable(typeof(AnalyzeSummary))]
[JsonSerializable(typeof(Diagnostic))]
[JsonSerializable(typeof(IReadOnlyList<Diagnostic>))]
public partial class CodeCloneJsonContext : JsonSerializerContext
{
    /// <summary>
    /// Default options for deserializing CodeClone output.
    /// </summary>
    public static JsonSerializerOptions DefaultOptions { get; } = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };
}

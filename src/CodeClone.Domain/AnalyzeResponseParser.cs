using System.Text.Json;

namespace CodeClone.Domain;

/// <summary>
/// Parses codeclone CLI JSON output into domain models.
/// </summary>
public static class AnalyzeResponseParser
{
    /// <summary>
    /// Parse JSON string into AnalyzeResponse.
    /// </summary>
    public static AnalyzeResponse Parse(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);

        return JsonSerializer.Deserialize<AnalyzeResponse>(json, CodeCloneJsonContext.DefaultOptions)
            ?? throw new JsonException("Failed to deserialize CodeClone response");
    }

    /// <summary>
    /// Try to parse JSON string into AnalyzeResponse.
    /// </summary>
    public static bool TryParse(string json, out AnalyzeResponse? result, out string? error)
    {
        result = null;
        error = null;

        if (string.IsNullOrWhiteSpace(json))
        {
            error = "JSON input is empty";
            return false;
        }

        try
        {
            result = JsonSerializer.Deserialize<AnalyzeResponse>(json, CodeCloneJsonContext.DefaultOptions);
            if (result is null)
            {
                error = "Deserialization returned null";
                return false;
            }
            return true;
        }
        catch (JsonException ex)
        {
            error = ex.Message;
            return false;
        }
    }
}

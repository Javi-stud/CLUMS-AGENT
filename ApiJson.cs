using System.Text.Json;
using System.Text.Json.Serialization;

namespace LabMonitorAgent;

/// <summary>One JSON style for everything: snake_case names (matches the server), no nulls.</summary>
public static class ApiJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };

    public static readonly JsonSerializerOptions Indented = new(Options) { WriteIndented = true };
}
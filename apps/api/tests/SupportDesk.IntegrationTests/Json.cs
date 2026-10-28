using System.Text.Json;
using System.Text.Json.Serialization;

namespace SupportDesk.IntegrationTests;

/// <summary>Reads responses the way the API writes them: camelCase, enums as names.</summary>
internal static class Json
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };
}

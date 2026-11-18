using System.Text.Json;

namespace TuitionAssistant.Api.Tests.Infrastructure;

public static class HttpJson
{
    public static async Task<JsonElement> ReadJsonAsync(this HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
}

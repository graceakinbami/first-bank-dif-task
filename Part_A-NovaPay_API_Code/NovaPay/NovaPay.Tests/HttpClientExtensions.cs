using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace NovaPay.Tests;

public static class HttpClientExtensions
{
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static HttpClient WithBearerToken(this HttpClient client, string token)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    public static Task<HttpResponseMessage> PostTransferAsync(
        this HttpClient client, object body, string? idempotencyKey)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/transfers")
        {
            Content = JsonContent.Create(body, options: JsonOptions)
        };
        if (idempotencyKey is not null)
            request.Headers.Add("Idempotency-Key", idempotencyKey);

        return client.SendAsync(request);
    }
}

using System.Net.Http;
using System.Text;
using Serilog;
using TouchDeck.Core.Abstractions;

namespace TouchDeck.Platform.Services;

/// <summary>
/// Sends web requests, so a button can poke anything with an HTTP API without the deck
/// needing to know what it is.
/// </summary>
public sealed class HttpSender : IHttpSender, IDisposable
{
    private readonly HttpClient _client = new();
    private readonly ILogger _logger;

    /// <summary>Creates a sender.</summary>
    /// <param name="logger">Where requests are recorded.</param>
    public HttpSender(ILogger logger)
    {
        _logger = logger.ForContext<HttpSender>();
        _client.Timeout = TimeSpan.FromSeconds(30);
    }

    /// <inheritdoc />
    public async Task<string> SendAsync(
        string method,
        string url,
        IReadOnlyDictionary<string, string> headers,
        string? body,
        CancellationToken ct)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            throw new ArgumentException($"\"{url}\" is not a web address.", nameof(url));
        }

        using var request = new HttpRequestMessage(new HttpMethod(method.ToUpperInvariant()), uri);

        if (body is { Length: > 0 })
        {
            request.Content = new StringContent(body, Encoding.UTF8, ContentTypeOf(headers, body));
        }

        foreach (var (name, value) in headers)
        {
            if (name.Equals("content-type", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            request.Headers.TryAddWithoutValidation(name, value);
        }

        using var response = await _client.SendAsync(request, ct).ConfigureAwait(false);
        var text = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

        _logger.Information("{Method} {Url} returned {Status}", request.Method, uri, (int)response.StatusCode);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"{uri} answered {(int)response.StatusCode} {response.ReasonPhrase}.");
        }

        return text;
    }

    /// <inheritdoc />
    public void Dispose() => _client.Dispose();

    /// <summary>Uses the stated content type, or guesses json when the body looks like json.</summary>
    private static string ContentTypeOf(IReadOnlyDictionary<string, string> headers, string body)
    {
        foreach (var (name, value) in headers)
        {
            if (name.Equals("content-type", StringComparison.OrdinalIgnoreCase))
            {
                return value;
            }
        }

        var trimmed = body.TrimStart();
        return trimmed.StartsWith('{') || trimmed.StartsWith('[')
            ? "application/json"
            : "text/plain";
    }
}

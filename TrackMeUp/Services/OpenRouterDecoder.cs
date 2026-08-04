using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace TrackMeUp.Services;

/// <summary>
/// OpenRouter adapter for text + image analysis requests.
/// </summary>
public sealed class OpenRouterDecoder : IAIDecoder
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(2) };

    /// <summary>
    /// Provider name.
    /// </summary>
    public string Provider => "openrouter";

    /// <summary>
    /// Sends one request to OpenRouter endpoint.
    /// </summary>
    public async Task<string> DecodeAsync(string prompt, IReadOnlyList<string> screenshotPaths, AppSettings settings, string apiKey)
    {
        var imageDataUrls = new List<string>();
        foreach (var screenshot in screenshotPaths)
        {
            var image = Convert.ToBase64String(await File.ReadAllBytesAsync(screenshot));
            imageDataUrls.Add($"data:image/webp;base64,{image}");
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, settings.AiEndpoint);
        // OpenRouter requires standard bearer auth plus optional app metadata.
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        request.Headers.Add("HTTP-Referer", "https://trackmeup.local");
        request.Headers.Add("X-Title", "TrackMeUp");
        request.Content = new StringContent(SerializePayload(prompt, imageDataUrls, settings), Encoding.UTF8, "application/json");

        using var response = await Http.SendAsync(request);
        var responseBody = await response.Content.ReadAsStringAsync();
        // Keep failures explicit: return provider message first, then fallback generic status.
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(ReadApiError(responseBody) ?? $"OpenRouter ha restituito {(int)response.StatusCode}.");
        }

        return ReadOutputText(responseBody) ?? "Il modello non ha restituito testo.";
    }

    internal static string SerializePayload(string prompt, IReadOnlyList<string> imageDataUrls, AppSettings settings)
    {
        var profile = AiAnalysisProfileCatalog.Resolve(settings.AiOutputDetail);
        var content = new List<object>
        {
            new Dictionary<string, object?>
            {
                ["type"] = "text",
                ["text"] = prompt
            }
        };

        foreach (var imageDataUrl in imageDataUrls)
        {
            content.Add(new Dictionary<string, object?>
            {
                ["type"] = "image_url",
                ["image_url"] = new Dictionary<string, object?>
                {
                    ["url"] = imageDataUrl,
                    ["detail"] = profile.ImageDetail
                }
            });
        }

        var payload = new Dictionary<string, object?>
        {
            ["model"] = settings.Model,
            ["messages"] = new[]
            {
                new Dictionary<string, object?>
                {
                    ["role"] = "user",
                    ["content"] = content
                }
            },
            ["max_tokens"] = profile.MaxOutputTokens
        };

        return JsonSerializer.Serialize(payload);
    }

    /// <summary>
    /// Parses text from OpenRouter-style responses.
    /// </summary>
    private static string? ReadOutputText(string json)
    {
        using var document = JsonDocument.Parse(json);

        if (document.RootElement.TryGetProperty("choices", out var choices))
        {
            foreach (var item in choices.EnumerateArray())
            {
                if (!item.TryGetProperty("message", out var message)) continue;
                if (message.TryGetProperty("content", out var content))
                {
                    return content.ValueKind == JsonValueKind.Array ? ReadFirstTextFromArray(content) : content.GetString();
                }
            }
        }

        if (document.RootElement.TryGetProperty("output", out var output))
        {
            foreach (var item in output.EnumerateArray())
            {
                if (!item.TryGetProperty("content", out var content)) continue;
                foreach (var part in content.EnumerateArray())
                {
                    if (part.TryGetProperty("type", out var type) && type.GetString() == "output_text" && part.TryGetProperty("text", out var text))
                    {
                        return text.GetString();
                    }
                }
            }
        }

        return null;
    }

    private static string? ReadFirstTextFromArray(JsonElement content)
    {
        foreach (var part in content.EnumerateArray())
        {
            if (part.TryGetProperty("type", out var type) && type.GetString() == "text" && part.TryGetProperty("text", out var text))
            {
                return text.GetString();
            }
        }

        return null;
    }

    /// <summary>
    /// Reads error payload for actionable details.
    /// </summary>
    private static string? ReadApiError(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.TryGetProperty("error", out var error)
                ? error.TryGetProperty("message", out var message) ? message.GetString() : error.GetString()
                : null;
        }
        catch
        {
            return null;
        }
    }
}

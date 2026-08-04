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
/// Anthropic adapter for text + image analysis requests.
/// </summary>
public sealed class AnthropicDecoder : IAIDecoder
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(2) };

    /// <summary>
    /// Provider name.
    /// </summary>
    public string Provider => "anthropic";

    /// <summary>
    /// Sends one request to Anthropic endpoint.
    /// </summary>
    public async Task<string> DecodeAsync(string prompt, IReadOnlyList<string> screenshotPaths, AppSettings settings, string apiKey)
    {
        var base64Images = new List<string>();
        foreach (var screenshot in screenshotPaths)
        {
            var image = Convert.ToBase64String(await File.ReadAllBytesAsync(screenshot));
            base64Images.Add(image);
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, settings.AiEndpoint);
        // Keep auth + protocol version explicit for stable response behavior.
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        request.Headers.Add("anthropic-version", "2023-06-01");
        request.Content = new StringContent(SerializePayload(prompt, base64Images, settings), Encoding.UTF8, "application/json");

        using var response = await Http.SendAsync(request);
        var responseBody = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(ReadApiError(responseBody) ?? $"Anthropic ha restituito {(int)response.StatusCode}.");
        }

        return ReadOutputText(responseBody) ?? "Il modello non ha restituito testo.";
    }

    internal static string SerializePayload(string prompt, IReadOnlyList<string> base64Images, AppSettings settings)
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

        foreach (var base64Image in base64Images)
        {
            content.Add(new Dictionary<string, object?>
            {
                ["type"] = "image",
                ["source"] = new Dictionary<string, object?>
                {
                    ["type"] = "base64",
                    ["media_type"] = "image/webp",
                    ["data"] = base64Image
                }
            });
        }

        var payload = new Dictionary<string, object?>
        {
            ["model"] = settings.Model,
            ["max_tokens"] = profile.MaxOutputTokens,
            ["messages"] = new[]
            {
                new Dictionary<string, object?>
                {
                    ["role"] = "user",
                    ["content"] = content
                }
            }
        };

        return JsonSerializer.Serialize(payload);
    }

    private static string? ReadOutputText(string json)
    {
        using var document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("content", out var content))
        {
            return null;
        }

        foreach (var item in content.EnumerateArray())
        {
            if (item.TryGetProperty("type", out var type) && type.GetString() == "text" && item.TryGetProperty("text", out var text))
            {
                return text.GetString();
            }
        }
        return null;
    }

    /// <summary>
    /// Reads error payload from provider response.
    /// </summary>
    private static string? ReadApiError(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.TryGetProperty("error", out var error))
            {
                if (error.TryGetProperty("message", out var message))
                {
                    return message.GetString();
                }

                if (error.TryGetProperty("error", out var nested) && nested.TryGetProperty("message", out var nestedMessage))
                {
                    return nestedMessage.GetString();
                }
            }
        }
        catch
        {
        }

        return null;
    }
}

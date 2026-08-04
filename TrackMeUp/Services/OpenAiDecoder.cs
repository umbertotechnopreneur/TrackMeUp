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
/// OpenAI-compatible decoder for text + image payloads.
/// </summary>
public sealed class OpenAiDecoder : IAIDecoder
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(2) };

    /// <summary>
    /// Provider name.
    /// </summary>
    public string Provider => "openai";

    /// <summary>
    /// Sends one analysis request to the configured OpenAI endpoint.
    /// </summary>
    /// <param name="prompt">Prompt to send.</param>
    /// <param name="screenshotPaths">Optional screenshot paths.</param>
    /// <param name="settings">Current settings.</param>
    /// <param name="apiKey">Resolved API key.</param>
    /// <returns>Model output text.</returns>
    public async Task<string> DecodeAsync(string prompt, IReadOnlyList<string> screenshotPaths, AppSettings settings, string apiKey)
    {
        var imageDataUrls = new List<string>();
        foreach (var screenshot in screenshotPaths)
        {
            var image = Convert.ToBase64String(await File.ReadAllBytesAsync(screenshot));
            imageDataUrls.Add($"data:image/webp;base64,{image}");
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, settings.AiEndpoint);

        // Keep auth/configuration in request headers so body remains deterministic for snapshots.
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        request.Content = new StringContent(SerializePayload(prompt, imageDataUrls, settings), Encoding.UTF8, "application/json");

        using var response = await Http.SendAsync(request);
        var responseBody = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(ReadApiError(responseBody) ?? $"Provider OpenAI ha restituito {(int)response.StatusCode}.");
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
                ["type"] = "input_text",
                ["text"] = prompt
            }
        };

        foreach (var imageDataUrl in imageDataUrls)
        {
            content.Add(new Dictionary<string, object?>
            {
                ["type"] = "input_image",
                ["image_url"] = imageDataUrl,
                ["detail"] = profile.ImageDetail
            });
        }

        var payload = new Dictionary<string, object?>
        {
            ["model"] = settings.Model,
            ["input"] = new[]
            {
                new Dictionary<string, object?>
                {
                    ["role"] = "user",
                    ["content"] = content
                }
            },
            ["max_output_tokens"] = profile.MaxOutputTokens,
            ["text"] = new Dictionary<string, object?>
            {
                ["verbosity"] = profile.TextVerbosity
            }
        };

        var reasoningEffort = AiAnalysisProfileCatalog.ResolveReasoningEffort(settings.AiReasoningEffort);
        if (reasoningEffort is not null)
        {
            payload["reasoning"] = new Dictionary<string, object?>
            {
                ["effort"] = reasoningEffort
            };
        }

        return JsonSerializer.Serialize(payload);
    }

    /// <summary>
    /// Parses the textual output from OpenAI structured responses.
    /// </summary>
    private static string? ReadOutputText(string json)
    {
        using var document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("output", out var output))
        {
            return null;
        }

        foreach (var item in output.EnumerateArray())
        {
            if (!item.TryGetProperty("content", out var content))
            {
                continue;
            }

            foreach (var part in content.EnumerateArray())
            {
                if (!part.TryGetProperty("type", out var type) || type.GetString() != "output_text")
                {
                    continue;
                }

                if (part.TryGetProperty("text", out var text))
                {
                    return text.GetString();
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Reads provider error details from response payload.
    /// </summary>
    private static string? ReadApiError(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.GetProperty("error").GetProperty("message").GetString();
        }
        catch
        {
            return null;
        }
    }
}

using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace WarmachineClient.Api;

public class WarmachineApiClient
{
    private readonly HttpClient _http;
    private readonly JsonSerializerOptions _jsonOptions;

    public WarmachineApiClient(string baseUrl, string? apiKey = null)
    {
        _http = new HttpClient { BaseAddress = new Uri(baseUrl) };
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            _http.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
        }

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        };
        _jsonOptions.Converters.Add(new JsonStringEnumConverter());
    }

    public async Task<bool> CheckHealthAsync()
    {
        using var response = await _http.GetAsync("/health");
        return response.IsSuccessStatusCode;
    }

    public async Task<T> GetAsync<T>(string path)
    {
        using var response = await _http.GetAsync(path);
        await EnsureSuccessAsync(response);
        return await DeserializeAsync<T>(response);
    }

    public async Task<T> PostAsync<T>(string path, object body)
    {
        using var response = await _http.PostAsync(path, ToJsonContent(body));
        await EnsureSuccessAsync(response);
        return await DeserializeAsync<T>(response);
    }

    public async Task PutAsync(string path, object body)
    {
        using var response = await _http.PutAsync(path, ToJsonContent(body));
        await EnsureSuccessAsync(response);
    }

    public async Task<T> PatchAsync<T>(string path, object body)
    {
        using var response = await _http.PatchAsync(path, ToJsonContent(body));
        await EnsureSuccessAsync(response);
        return await DeserializeAsync<T>(response);
    }

    public async Task DeleteAsync(string path)
    {
        using var response = await _http.DeleteAsync(path);
        await EnsureSuccessAsync(response);
    }

    private HttpContent ToJsonContent(object body)
    {
        var json = JsonSerializer.Serialize(body, _jsonOptions);
        return new StringContent(json, Encoding.UTF8, "application/json");
    }

    private async Task<T> DeserializeAsync<T>(HttpResponseMessage response)
    {
        var stream = await response.Content.ReadAsStreamAsync();
        var result = await JsonSerializer.DeserializeAsync<T>(stream, _jsonOptions);
        return result!;
    }

    private async Task EnsureSuccessAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        string? title = null;
        string? detail = null;
        var body = await response.Content.ReadAsStringAsync();
        if (!string.IsNullOrWhiteSpace(body))
        {
            try
            {
                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("title", out var titleProp))
                {
                    title = titleProp.GetString();
                }

                if (doc.RootElement.TryGetProperty("detail", out var detailProp))
                {
                    detail = detailProp.GetString();
                }
            }
            catch (JsonException)
            {
                detail = body;
            }
        }

        throw new ApiException((int)response.StatusCode, title, detail);
    }
}

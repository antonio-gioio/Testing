using Blazored.LocalStorage;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace ContainerTracking.Web;

public class ApiClient
{
    private readonly HttpClient _http;
    private readonly ILocalStorageService _storage;

    public ApiClient(HttpClient http, ILocalStorageService storage)
    {
        _http = http;
        _storage = storage;
    }

    private async Task SetAuthHeaderAsync()
    {
        var token = await _storage.GetItemAsStringAsync("auth_token");
        if (!string.IsNullOrEmpty(token))
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    public async Task<T?> GetAsync<T>(string path)
    {
        await SetAuthHeaderAsync();
        return await _http.GetFromJsonAsync<T>(path);
    }

    public async Task<HttpResponseMessage> PostAsync<T>(string path, T body)
    {
        await SetAuthHeaderAsync();
        return await _http.PostAsJsonAsync(path, body);
    }

    public async Task<HttpResponseMessage> PutAsync<T>(string path, T body)
    {
        await SetAuthHeaderAsync();
        return await _http.PutAsJsonAsync(path, body);
    }

    public async Task<HttpResponseMessage> DeleteAsync(string path)
    {
        await SetAuthHeaderAsync();
        return await _http.DeleteAsync(path);
    }

    public async Task<LoginResult?> LoginAsync(string email, string password)
    {
        var response = await _http.PostAsJsonAsync("api/v1/auth/login", new { email, password });
        if (!response.IsSuccessStatusCode) return null;

        var result = await response.Content.ReadFromJsonAsync<LoginResult>();
        if (result != null)
        {
            await _storage.SetItemAsStringAsync("auth_token", result.AccessToken);
            await _storage.SetItemAsStringAsync("org_id", result.OrganizationId.ToString());
        }
        return result;
    }

    public async Task LogoutAsync()
    {
        await _storage.RemoveItemAsync("auth_token");
        await _storage.RemoveItemAsync("org_id");
    }
}

public record LoginResult(string AccessToken, string RefreshToken, DateTime ExpiresAt, Guid UserId, Guid OrganizationId);

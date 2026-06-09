using ChzzkApi_CS.Session;
using Microsoft.AspNetCore.WebUtilities;
using System.Collections.Specialized;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;

namespace ChzzkApi_CS;

public class ChzzkClientApi
{
    private HttpClient Client { get; init; }

    public string ClientId { get; private set; }
    public string ClientSecret { get; private set; }

    public ChzzkClientApi(HttpClient httpClient, string clientId, string clientSecret)
    {
        Client = httpClient;
        ClientId = clientId;
        ClientSecret = clientSecret;
    }

    public void SetCredentials(string clientId, string clientSecret)
    {

        ClientId = clientId;
        ClientSecret = clientSecret;
    }
    
    public async Task<T> SendCoreAsync<T>(HttpRequestMessage request, CancellationToken ct)
    {
        using var response = await Client.SendAsync(request, ct);
        await using var stream = await response.Content.ReadAsStreamAsync(ct);

        //Console.WriteLine(await response.Content.ReadAsStringAsync(ct));

        return await JsonSerializer.DeserializeAsync<T>(stream, cancellationToken: ct)
            ?? throw new InvalidOperationException("Chzzk API response deserialized to null.");
    }

    private Task<T> SendClientCoreAsync<T>(HttpRequestMessage request, CancellationToken ct = default)
    {
        request.Headers.Add("Client-Id", ClientId);
        request.Headers.Add("Client-Secret", ClientSecret);

        return SendCoreAsync<T>(request, ct);
    }

    public Task<ChzzkResponse<T>> GetClientAsync<T>(string uri, CancellationToken ct = default)
        => SendClientCoreAsync<ChzzkResponse<T>>(new HttpRequestMessage(HttpMethod.Get, uri), ct);

    public Task<ChzzkResponse> GetClientAsync(string uri, CancellationToken ct = default)
        => SendClientCoreAsync<ChzzkResponse>(new HttpRequestMessage(HttpMethod.Get, uri), ct);

    public Task<ChzzkResponse<T>> PostClientAsync<T>(string uri, HttpContent? content, CancellationToken ct = default)
        => SendClientCoreAsync<ChzzkResponse<T>>(new HttpRequestMessage(HttpMethod.Post, uri) { Content = content }, ct);

    public Task<ChzzkResponse> PostClientAsync(string uri, HttpContent? content, CancellationToken ct = default)
        => SendClientCoreAsync<ChzzkResponse>(new HttpRequestMessage(HttpMethod.Post, uri) { Content = content }, ct);

    #region Authorization

    public string GetAuthorizationUri(string redirectUri, string state)
    {
        return QueryHelpers.AddQueryString("https://chzzk.naver.com/account-interlock",
            new Dictionary<string, string?>
            {
                ["clientId"] = ClientId,
                ["redirectUri"] = redirectUri,
                ["state"] = state
            });
    }

    public string GetAuthorizationUri(string redirectUri, out string state)
    {
        state = GetRandomString(12);

        return GetAuthorizationUri(redirectUri, state);
    }

    public static async Task<string> WaitForAuthorizationCodeAsync(
        string redirectUri, 
        string expectedState,
        Func<HttpListenerResponse, NameValueCollection, Task>? configureResponse = null,
        CancellationToken ct = default)
    {
        using var listener = new HttpListener();
        listener.Prefixes.Add(redirectUri);
        listener.Start();

        using var registration = ct.Register(listener.Close);

        while (true)
        {
            HttpListenerContext context;
            try
            {
                context = await listener.GetContextAsync();
            }
            catch (HttpListenerException) when (ct.IsCancellationRequested)
            {
                ct.ThrowIfCancellationRequested();
                throw;
            }
            catch (ObjectDisposedException) when (ct.IsCancellationRequested)
            {
                ct.ThrowIfCancellationRequested();
                throw;
            }

            var query = context.Request.QueryString;

            if (configureResponse != null)
                await configureResponse(context.Response, query);

            context.Response.Close();

            if (query["state"] != expectedState)
                continue;

            return query["code"]!;
        }
    }

    public async Task<ChzzkUserApi> IssueAccessTokenAsync(HttpClient client, string code, string state, CancellationToken ct = default)
    {
        var requestBody = new
        {
            grantType = "authorization_code",
            clientId = ClientId,
            clientSecret = ClientSecret,
            code = code,
            state = state
        };
        
        using var response = await Client.PostAsJsonAsync("/auth/v1/token", requestBody, ct);

        await using var stream = await response.Content.ReadAsStreamAsync(ct);

        var wrapper = await JsonSerializer.DeserializeAsync<ChzzkResponse<TokenContent>>(stream, cancellationToken: ct)
            ?? throw new InvalidOperationException("Chzzk API response deserialized to null.");

        if (wrapper.Code != ChzzkStatusCode.Success)
            throw new ChzzkApiException(wrapper);

        return new(this, wrapper.Content!.AccessToken, wrapper.Content.RefreshToken);
    }

    public async Task<ChzzkResponse<TokenContent>> RefreshAccessTokenAsync(string refreshToken, CancellationToken ct = default)
    {
        var requestBody = new
        {
            grantType = "refresh_token",
            refreshToken = refreshToken,
            clientId = ClientId,
            clientSecret = ClientSecret
        };

        using var response = await Client.PostAsJsonAsync("/auth/v1/token", requestBody, ct);
        
        await using var stream = await response.Content.ReadAsStreamAsync(ct);

        var wrapper = await JsonSerializer.DeserializeAsync<ChzzkResponse<TokenContent>>(stream, cancellationToken: ct)
            ?? throw new InvalidOperationException("Chzzk API response deserialized to null.");

        return wrapper;
    }

    public async Task<ChzzkResponse> RevokeAccessTokenAsync(string token, string tokenTypeHint = "access_token", CancellationToken ct = default)
    {
        var requestBody = new
        {
            clientId = ClientId,
            clientSecret = ClientSecret,
            token = token,
            tokenTypeHint = tokenTypeHint
        };

        using var response = await Client.PostAsJsonAsync("/auth/v1/token/revoke", requestBody, ct);
        await using var stream = await response.Content.ReadAsStreamAsync(ct);

        var wrapper = await JsonSerializer.DeserializeAsync<ChzzkResponse>(stream, cancellationToken: ct);

        if (wrapper is null)
            throw new InvalidOperationException("Chzzk API response deserialized to null.");

        return wrapper;
    }

    #endregion Authorization
    #region Session

    public async Task<ChzzkResponse<SessionListData>> GetSessionListAsync(int size = 20, string page = "0", CancellationToken ct = default)
    {
        var uri = QueryHelpers.AddQueryString("/open/v1/sessions/client",
            new Dictionary<string, string?>
            {
                ["size"] = size.ToString(),
                ["page"] = page
            });

        var response = await GetClientAsync<SessionListData>(uri, ct);

        return response;
    }

    public async Task<ChzzkSession> CreateSessionAsync(CancellationToken ct = default)
    {
        var response = await GetClientAsync<SessionCreateContent>("/open/v1/sessions/auth/client", ct);

        if (response.Code != ChzzkStatusCode.Success)
            throw new ChzzkApiException(response);

        return new ChzzkSession(new Uri(response.Content!.Url));
    }

    #endregion Session
    #region Category
    #endregion Category
    #region Live
    #endregion Live

    private static readonly char[] chars =
        "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890".ToCharArray();

    public static string GetRandomString(int size)
    {
        return string.Create(size, chars,
            static (span, chars) =>
            {
                for (int i = 0; i < span.Length; i++)
                {
                    span[i] = chars[RandomNumberGenerator.GetInt32(chars.Length)];
                }
            }
        );
    }
}
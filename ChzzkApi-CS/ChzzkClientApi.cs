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
        using var response = await Client.SendAsync(request, ct).ConfigureAwait(false);

        // Chzzk reports API-level failures inside the JSON envelope, so the HTTP status alone
        // is not an error. Gateways and proxies, however, answer with HTML or an empty body.
        string body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

        try
        {
            return JsonSerializer.Deserialize<T>(body)
                ?? throw new ChzzkApiException((int)response.StatusCode, "Chzzk API returned an empty response body.");
        }
        catch (JsonException ex)
        {
            throw new ChzzkApiException((int)response.StatusCode, $"Chzzk API returned a non-JSON response: {Summarize(body)}", ex);
        }
    }

    private static string Summarize(string body)
        => body.Length <= 200 ? body : $"{body[..200]}...";

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
                await configureResponse(context.Response, query).ConfigureAwait(false);

            context.Response.Close();

            if (query["state"] != expectedState)
                continue;

            if (query["error"] is string error)
                throw new InvalidOperationException($"Authorization was denied: {error} {query["error_description"]}".TrimEnd());

            return query["code"]
                ?? throw new InvalidOperationException("Authorization callback carried no code.");
        }
    }

    private Task<T> PostAuthAsync<T>(string uri, object requestBody, CancellationToken ct)
        => SendCoreAsync<T>(new HttpRequestMessage(HttpMethod.Post, uri) { Content = JsonContent.Create(requestBody) }, ct);

    public async Task<ChzzkUserApi> IssueAccessTokenAsync(string code, string state, CancellationToken ct = default)
    {
        var requestBody = new
        {
            grantType = "authorization_code",
            clientId = ClientId,
            clientSecret = ClientSecret,
            code = code,
            state = state
        };

        var wrapper = await PostAuthAsync<ChzzkResponse<TokenContent>>("/auth/v1/token", requestBody, ct).ConfigureAwait(false);

        if (wrapper.Code != ChzzkStatusCode.Success)
            throw new ChzzkApiException(wrapper);

        return new(this, wrapper.Content!.AccessToken, wrapper.Content.RefreshToken);
    }

    public Task<ChzzkResponse<TokenContent>> RefreshAccessTokenAsync(string refreshToken, CancellationToken ct = default)
    {
        var requestBody = new
        {
            grantType = "refresh_token",
            refreshToken = refreshToken,
            clientId = ClientId,
            clientSecret = ClientSecret
        };

        return PostAuthAsync<ChzzkResponse<TokenContent>>("/auth/v1/token", requestBody, ct);
    }

    public Task<ChzzkResponse> RevokeAccessTokenAsync(string token, string tokenTypeHint = "access_token", CancellationToken ct = default)
    {
        var requestBody = new
        {
            clientId = ClientId,
            clientSecret = ClientSecret,
            token = token,
            tokenTypeHint = tokenTypeHint
        };

        return PostAuthAsync<ChzzkResponse>("/auth/v1/token/revoke", requestBody, ct);
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

        return await GetClientAsync<SessionListData>(uri, ct).ConfigureAwait(false);
    }

    public async Task<ChzzkSession> CreateSessionAsync(CancellationToken ct = default)
    {
        var response = await GetClientAsync<SessionCreateContent>("/open/v1/sessions/auth/client", ct).ConfigureAwait(false);

        if (response.Code != ChzzkStatusCode.Success)
            throw new ChzzkApiException(response);

        return new ChzzkSession(new Uri(response.Content!.Url),
            (uri, token) => PostClientAsync(uri, null, token));
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
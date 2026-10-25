using ChzzkApi.Session;
using Microsoft.AspNetCore.WebUtilities;
using System.Collections.Specialized;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;

namespace ChzzkApi;

public class ChzzkClient(IHttpClientFactory clientFactory, string clientId, string clientSecret)
{
    private const string Path = "https://openapi.chzzk.naver.com";

    private IHttpClientFactory ClientFactory { get; init; } = clientFactory;
    public string ClientId { get; } = clientId;
    private readonly string clientSecret = clientSecret;

    internal async Task<T> SendCoreAsync<T>(HttpMethod method, string pathAndQuery, HttpContent? content,
        Action<HttpRequestHeaders>? setHeaders, CancellationToken ct = default)
        where T : ChzzkResponse
    {
        var client = ClientFactory.CreateClient(nameof(ChzzkApi));

        using var request = new HttpRequestMessage(method, $"{Path}{pathAndQuery}") { Content = content };
        setHeaders?.Invoke(request.Headers);

        HttpResponseMessage response;
        try
        {
            response = await client.SendAsync(request, ct);
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            throw new TimeoutException(ex.Message, ex); ;
        }

        string body = await response.Content.ReadAsStringAsync(ct);
        response.Dispose();

        T? envelope;
        try
        {
            envelope = ChzzkResponse.Parse<T>(body);
        }
        catch (JsonException ex)
        {
            throw new ChzzkResponseException(response.StatusCode, body, ex);
        }

        if (envelope is null || envelope.Code == default)
            throw new ChzzkResponseException(response.StatusCode, body);

        return envelope;
    }

    private Task<T> SendClientCoreAsync<T>(HttpMethod method, string uri, HttpContent? content, CancellationToken ct = default)
        where T : ChzzkResponse
        => SendCoreAsync<T>(method, uri, content, headers =>
        {
            headers.Add("Client-Id", ClientId);
            headers.Add("Client-Secret", clientSecret);
        }, ct);

    public Task<ChzzkResponse<T>> GetClientAsync<T>(string uri, CancellationToken ct = default)
        => SendClientCoreAsync<ChzzkResponse<T>>(HttpMethod.Get, uri, null, ct);

    public Task<ChzzkResponse> GetClientAsync(string uri, CancellationToken ct = default)
        => SendClientCoreAsync<ChzzkResponse>(HttpMethod.Get, uri, null, ct);

    public Task<ChzzkResponse<T>> PostClientAsync<T>(string uri, HttpContent? content, CancellationToken ct = default)
        => SendClientCoreAsync<ChzzkResponse<T>>(HttpMethod.Post, uri, content, ct);

    public Task<ChzzkResponse> PostClientAsync(string uri, HttpContent? content, CancellationToken ct = default)
        => SendClientCoreAsync<ChzzkResponse>(HttpMethod.Post, uri, content, ct);

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

            if (query["error"] is string error)
                throw new InvalidOperationException($"Authorization was denied: {error} {query["error_description"]}".TrimEnd());

            return query["code"]!;
        }
    }

    private Task<T> PostAuthAsync<T>(string uri, object requestBody, CancellationToken ct)
        where T : ChzzkResponse
        => SendCoreAsync<T>(HttpMethod.Post, uri, JsonContent.Create(requestBody), null, ct);

    public async Task<ChzzkUser> IssueAccessTokenAsync(string code, string state, CancellationToken ct = default)
    {
        var requestBody = new
        {
            grantType = "authorization_code",
            clientId = ClientId,
            clientSecret,
            code,
            state
        };

        var wrapper = await PostAuthAsync<ChzzkResponse<TokenContent>>("/auth/v1/token", requestBody, ct);

        if (wrapper.Code != HttpStatusCode.OK)
            throw new ChzzkApiException(wrapper);

        return new(this, wrapper.Content!.AccessToken, wrapper.Content.RefreshToken);
    }

    public Task<ChzzkResponse<TokenContent>> RefreshAccessTokenAsync(string refreshToken, CancellationToken ct = default)
    {
        var requestBody = new
        {
            grantType = "refresh_token",
            refreshToken,
            clientId = ClientId,
            clientSecret
        };

        return PostAuthAsync<ChzzkResponse<TokenContent>>("/auth/v1/token", requestBody, ct);
    }

    public Task<ChzzkResponse> RevokeAccessTokenAsync(string token, string tokenTypeHint = "access_token", CancellationToken ct = default)
    {
        var requestBody = new
        {
            clientId = ClientId,
            clientSecret,
            token,
            tokenTypeHint
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

        return await GetClientAsync<SessionListData>(uri, ct);
    }

    public async Task<ChzzkSession> CreateSessionAsync(CancellationToken ct = default)
    {
        var response = await GetClientAsync<SessionCreateContent>("/open/v1/sessions/auth/client", ct);

        if (response.Code != HttpStatusCode.OK)
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
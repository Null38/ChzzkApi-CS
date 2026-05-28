using ChzzkApi_CS.Session;
using Microsoft.AspNetCore.WebUtilities;
using System.Collections.Specialized;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;

namespace ChzzkApi_CS;

public class ChzzkApi
{
    public class Factory(IHttpClientFactory httpClientFactory)
    {
        public ChzzkApi Create() =>
            new(httpClientFactory.CreateClient(nameof(ChzzkApi)), string.Empty, string.Empty);
        public ChzzkApi Create(string clientId, string clientSecret) =>
            new(httpClientFactory.CreateClient(nameof(ChzzkApi)), clientId, clientSecret);
    }

    public HttpClient Client { get; init; }

    public string ClientId { get; private set; }//get 할때 emtpy일때 예외를 던져버리는 게 좋을까?.
    private string ClientSecret { get; set; }


    public ChzzkApi(HttpClient httpClient, string clientId, string clientSecret)
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

    public async Task<ChzzkResponse<TokenContent>> IssueAccessTokenAsync(string code, string state, CancellationToken ct = default)
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

        var wrapper = await JsonSerializer.DeserializeAsync<ChzzkResponse<TokenContent>>(stream, cancellationToken: ct);

        if (wrapper is null)
            throw new InvalidOperationException("Chzzk API response deserialized to null.");

        return wrapper;
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

        var wrapper = await JsonSerializer.DeserializeAsync<ChzzkResponse<TokenContent>>(stream, cancellationToken: ct);

        if (wrapper is null)
            throw new InvalidOperationException("Chzzk API response deserialized to null.");

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

    public async Task<ChzzkResponse<SessionListData>> GetClientSessionListAsync(int size = 20, string page = "0", CancellationToken ct = default)
    {
        var uri = QueryHelpers.AddQueryString("/open/v1/sessions/client",
            new Dictionary<string, string?>
            {
                ["size"] = size.ToString(),
                ["page"] = page
            });

        var response = await SendWithClientAsync<SessionListData>(HttpMethod.Get, uri, ct);

        return response;
    }

    public async Task<ChzzkResponse<SessionListData>> GetUserSessionListAsync(string accessToken, int size = 20, string page = "0", CancellationToken ct = default)
    {
        var uri = QueryHelpers.AddQueryString("/open/v1/sessions",
            new Dictionary<string, string?>
            {
                ["size"] = size.ToString(),
                ["page"] = page
            });

        var response = await SendWithAccessTokenAsync<SessionListData>(HttpMethod.Get, uri, accessToken, ct);

        return response;
    }

    public async Task<ChzzkSession> CreateClientAsync(CancellationToken ct = default)
    {
        var response = await SendWithClientAsync<SessionCreateContent>(HttpMethod.Get, "/open/v1/sessions/auth/client", ct);

        if (response.Code != ChzzkStatusCode.Success)
            throw new ChzzkApiException(response);

        return new ChzzkSession(this, new Uri(response.Content!.Url));
    }

    public async Task<ChzzkSession> CreateUserAsync(string accessToken, CancellationToken ct = default)
    {
        var response = await SendWithAccessTokenAsync<SessionCreateContent>(HttpMethod.Get, "/open/v1/sessions/auth", accessToken, ct);

        if (response.Code != ChzzkStatusCode.Success)
            throw new ChzzkApiException(response);

        return new ChzzkSession(this, new Uri(response.Content!.Url));
    }

    #endregion Session
    #region User

    public async Task<ChzzkResponse<UserInfo>> GetUserInfoAsync(string accessToken, CancellationToken ct = default)
    {
        var response = await SendWithAccessTokenAsync<UserInfo>(HttpMethod.Get, "/open/v1/users/me", accessToken, ct);

        return response;
    }

    #endregion User
    #region Channel
    /*
    public async Task<ChzzkResponse<ChannelInfo>> GetChannelInfoAsync(string accessToken, CancellationToken ct = default)
    {
        var response = await SendWithAccessTokenAsync<ChannelInfo>(HttpMethod.Get, "/open/v1/channels", accessToken, ct);

        return response;
    }
    */
    #endregion Channel
    #region Category
    #endregion Category
    #region Live
    #endregion Live
    #region Chat
    #endregion Chat
    #region Restriction
    #endregion Restriction

    public Task<ChzzkResponse<T>> SendWithAccessTokenAsync<T>(HttpMethod method, string requestUri, string accessToken, CancellationToken ct = default)
    {
        var request = new HttpRequestMessage(method, requestUri);

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        return SendInternalAsync<ChzzkResponse<T>>(request, ct);
    }

    public Task<ChzzkResponse> SendWithAccessTokenAsync(HttpMethod method, string requestUri, string accessToken, CancellationToken ct = default)
    {
        var request = new HttpRequestMessage(method, requestUri);

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        return SendInternalAsync<ChzzkResponse>(request, ct);
    }

    public Task<ChzzkResponse<T>> SendWithClientAsync<T>(HttpMethod method, string requestUri, CancellationToken ct = default)
    {
        var request = new HttpRequestMessage(method, requestUri);

        request.Headers.Add("Client-Id", ClientId);
        request.Headers.Add("Client-Secret", ClientSecret);

        return SendInternalAsync<ChzzkResponse<T>>(request, ct);
    }

    private async Task<T> SendInternalAsync<T>(HttpRequestMessage request, CancellationToken ct)
    {
        using var response = await Client.SendAsync(request, ct);
        await using var stream = await response.Content.ReadAsStreamAsync(ct);

        var wrapper = await JsonSerializer.DeserializeAsync<T>(stream, cancellationToken: ct);

        if (wrapper is null)
            throw new InvalidOperationException("Chzzk API response deserialized to null.");

        return wrapper;
    }

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
global using Query = System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<string, string?>>;

using ChzzkApi.Session;
using ChzzkApi.Exceptions;
using ChzzkApi.Models;
using ChzzkApi.Rule;
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

    private static readonly AsyncLocal<bool> inRule = new();

    public RuleList Rules { get; } = new();

    public static IDisposable SuppressRules()
    {
        var previous = inRule.Value;
        inRule.Value = true;
        return new RuleScope(previous);
    }

    private class RuleScope(bool previous) : IDisposable
    {
        public void Dispose() => inRule.Value = previous;
    }

    #region Request Pipeline

    internal async Task<T> SendCoreAsync<T>(HttpMethod method, string path, Query? query, object? body,
        Action<HttpRequestHeaders>? setHeaders, CancellationToken ct = default)
        where T : ChzzkResponse
    {
        var client = ClientFactory.CreateClient(nameof(ChzzkApi));

        using var request = new HttpRequestMessage(method, $"{Path}{(query is null ? path : QueryHelpers.AddQueryString(path, query))}")
        {
            Content = body is null ? null : JsonContent.Create(body)
        };
        setHeaders?.Invoke(request.Headers);

        HttpResponseMessage response;
        try
        {
            response = await client.SendAsync(request, ct);
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            throw new TimeoutException(ex.Message, ex);
        }

        string raw = await response.Content.ReadAsStringAsync(ct);
        response.Dispose();

        T? envelope;
        try
        {
            envelope = ChzzkResponse.Parse<T>(raw);
        }
        catch (JsonException ex)
        {
            throw new ChzzkResponseException(response.StatusCode, raw, ex);
        }

        if (envelope is null || envelope.Code == default)
            throw new ChzzkResponseException(response.StatusCode, raw);

        return envelope;
    }

    internal async Task<T> SendWithRulesAsync<T>(HttpMethod method, string path, Query? query, object? body,
        Action<HttpRequestHeaders>? setHeaders, ChzzkUser? user = null, CancellationToken ct = default)
        where T : ChzzkResponse
    {
        var token = user?.Token;
        var response = await SendCoreAsync<T>(method, path, query, body, setHeaders, ct);

        if (inRule.Value)
            return response;
        inRule.Value = true;

        var context = new ChzzkRuleContext(response, this, user, token, async resendCt =>
        {
            var resent = await SendCoreAsync<T>(method, path, query, body, setHeaders, resendCt);
            return resent;
        });

        foreach (var rule in GetRules(user))
            await rule(context, ct);

        return (T)context.Response;
    }

    private List<ChzzkRule> GetRules(ChzzkUser? user)
    {
        List<ChzzkRule> result = [];

        foreach (var entry in Rules.Entries)
        {
            if (entry.Key is not null && user is not null && user.Rules.TryGet(entry.Key, out var userEntry))
                result.Add(userEntry.Rule);
            else
                result.Add(entry.Rule);
        }

        if (user is not null)
        {
            foreach (var entry in user.Rules.Entries)
            {
                if (entry.Key is null || !Rules.Contains(entry.Key))
                    result.Add(entry.Rule);
            }
        }

        return result;
    }

    public Task<ChzzkResponse<T>> SendClientAsync<T>(HttpMethod method, string path, Query? query = null, object? body = null, CancellationToken ct = default)
        => SendWithRulesAsync<ChzzkResponse<T>>(method, path, query, body, SetClientHeaders, ct: ct);

    public Task<ChzzkResponse> SendClientAsync(HttpMethod method, string path, Query? query = null, object? body = null, CancellationToken ct = default)
        => SendWithRulesAsync<ChzzkResponse>(method, path, query, body, SetClientHeaders, ct: ct);

    private void SetClientHeaders(HttpRequestHeaders headers)
    {
        headers.Add("Client-Id", ClientId);
        headers.Add("Client-Secret", clientSecret);
    }

    #endregion Request Pipeline

    #region Authorization

    public string BuildAuthUrl(string redirectUri, string state)
    {
        return QueryHelpers.AddQueryString("https://chzzk.naver.com/account-interlock",
            new Dictionary<string, string?>
            {
                ["clientId"] = ClientId,
                ["redirectUri"] = redirectUri,
                ["state"] = state
            });
    }

    public string BuildAuthUrl(string redirectUri, out string state)
    {
        state = GetRandomString(12);

        return BuildAuthUrl(redirectUri, state);
    }

    public static async Task<string> WaitForAuthCodeAsync(
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

            return query["code"]
                ?? throw new ChzzkAuthorizationException(query);
        }
    }

    private Task<T> PostAuthAsync<T>(string uri, object requestBody, CancellationToken ct)
        where T : ChzzkResponse
        => SendCoreAsync<T>(HttpMethod.Post, uri, null, requestBody, null, ct);

    public Task<ChzzkResponse<TokenContent>> IssueAccessTokenAsync(string code, string state, CancellationToken ct = default)
    {
        var requestBody = new
        {
            grantType = "authorization_code",
            clientId = ClientId,
            clientSecret,
            code,
            state
        };

        return PostAuthAsync<ChzzkResponse<TokenContent>>("/auth/v1/token", requestBody, ct);
    }

    public async Task<ChzzkUser> CreateUserAsync(string code, string state, CancellationToken ct = default)
    {
        var response = await IssueAccessTokenAsync(code, state, ct);

        if (response.Code != HttpStatusCode.OK)
            throw new ChzzkApiException(response);

        return new(this, new ChzzkUser.ChzzkToken
        {
            AccessToken = response.Content!.AccessToken,
            RefreshToken = response.Content.RefreshToken
        });
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

    public Task<ChzzkResponse<ChzzkPage<SessionInfo>>> GetSessionsAsync(int? size = null, int page = 0, CancellationToken ct = default)
        => SendClientAsync<ChzzkPage<SessionInfo>>(HttpMethod.Get, "/open/v1/sessions/client", query: [new("size", size?.ToString()), new("page", page.ToString())], ct: ct);

    public Task<ChzzkResponse<SessionUrlContent>> GetSessionUrlAsync(CancellationToken ct = default)
        => SendClientAsync<SessionUrlContent>(HttpMethod.Get, "/open/v1/sessions/auth/client", ct: ct);

    public async Task<ChzzkSession> CreateSessionAsync(CancellationToken ct = default)
    {
        var response = await GetSessionUrlAsync(ct);

        if (response.Code != HttpStatusCode.OK)
            throw new ChzzkApiException(response);

        return new ChzzkSession(new Uri(response.Content!.Url));
    }

    #endregion Session
    #region Channel

    public Task<ChzzkResponse<ChzzkPage<ChannelInfo>>> GetChannelsInfoAsync(string[] channelIds, CancellationToken ct = default)
        => SendClientAsync<ChzzkPage<ChannelInfo>> (HttpMethod.Get, "/open/v1/channels", query: channelIds.Select(id => new KeyValuePair<string, string?>("channelIds", id)), ct: ct);

    #endregion Channel
    #region Category

    public Task<ChzzkResponse<ChzzkPage<CategoryInfo>>> SearchCategoriesAsync(string query, int? size = null, CancellationToken ct = default)
        => SendClientAsync<ChzzkPage<CategoryInfo>>(HttpMethod.Get, "/open/v1/categories/search", query: [new("size", size?.ToString()), new("query", query)], ct: ct);

    #endregion Category
    #region Live

    public Task<ChzzkResponse<ChzzkCursorPage<LiveInfo>>> GetLivesAsync(int? size = null, string? next = null, CancellationToken ct = default)
        => SendClientAsync<ChzzkCursorPage<LiveInfo>>(HttpMethod.Get, "/open/v1/lives", query: [new("size", size?.ToString()), new("next", next)], ct: ct);

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
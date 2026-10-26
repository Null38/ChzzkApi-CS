using ChzzkApi.Session;
using System.Net;
using System.Net.Http.Headers;
using static ChzzkApi.ChzzkUser;

namespace ChzzkApi;

public class ChzzkUser(ChzzkClient clientApi, ChzzkToken token)
{
    public record ChzzkToken
    {
        public required string AccessToken { get; init; }
        public required string RefreshToken { get; init; }
    }

    private ChzzkClient ClientApi { get; init; } = clientApi;

    public ChzzkToken Token { get; private set; } = token;
    public RuleList Rules { get; } = new();

    readonly Lock refreshGate = new();
    Task<ChzzkResponse<TokenContent>>? refreshTask;

    public Task<ChzzkResponse<T>> SendUserAsync<T>(HttpMethod method, string path, Query? query = null, object? body = null, CancellationToken ct = default)
        => ClientApi.SendWithRulesAsync<ChzzkResponse<T>>(method, path, query, body, SetUserHeaders, this, ct);

    public Task<ChzzkResponse> SendUserAsync(HttpMethod method, string path, Query? query = null, object? body = null, CancellationToken ct = default)
        => ClientApi.SendWithRulesAsync<ChzzkResponse>(method, path, query, body, SetUserHeaders, this, ct);

    private void SetUserHeaders(HttpRequestHeaders headers)
        => headers.Authorization = new AuthenticationHeaderValue("Bearer", Token.AccessToken);


    #region Authorization

    public async Task<ChzzkResponse<TokenContent>> IssueTokenAsync(string code, string state, CancellationToken ct = default)
    {
        var response = await ClientApi.IssueAccessTokenAsync(code, state, ct);

        if (response.Code == HttpStatusCode.OK)
        {
            lock (refreshGate)
            {
                Token = new ChzzkToken
                {
                    AccessToken = response.Content!.AccessToken,
                    RefreshToken = response.Content.RefreshToken
                };
            }
        }

        return response;
    }

    private async Task<ChzzkResponse<TokenContent>> RefreshCoreAsync()
    {
        try
        {
            var sent = Token;
            var response = await ClientApi.RefreshAccessTokenAsync(sent.RefreshToken);

            if (response.Code == HttpStatusCode.OK)
            {
                lock (refreshGate)
                {
                    if (ReferenceEquals(Token, sent))
                    {
                        Token = new ChzzkToken
                        {
                            AccessToken = response.Content!.AccessToken,
                            RefreshToken = response.Content.RefreshToken
                        };
                    }
                }
            }

            return response;
        }
        finally
        {
            lock (refreshGate)
                refreshTask = null;
        }
    }

    public Task<ChzzkResponse<TokenContent>> RefreshAccessTokenAsync(CancellationToken ct = default)
    {
        lock (refreshGate)
            return (refreshTask ??= RefreshCoreAsync()).WaitAsync(ct);
    }

    public Task<ChzzkResponse> RevokeAccessTokenAsync(CancellationToken ct = default)
        => ClientApi.RevokeAccessTokenAsync(Token.AccessToken, ct: ct);

    #endregion Authorization
    #region Session

    public Task<ChzzkResponse<SessionListData>> GetSessionListAsync(int size = 20, string page = "0", CancellationToken ct = default)
        => SendUserAsync<SessionListData>(HttpMethod.Get, "/open/v1/sessions", [new("size", size.ToString()), new("page", page)], ct: ct);

    public Task<ChzzkResponse<SessionCreateContent>> GetSessionUrlAsync(CancellationToken ct = default)
        => SendUserAsync<SessionCreateContent>(HttpMethod.Get, "/open/v1/sessions/auth", ct: ct);

    public async Task<ChzzkSession> CreateSessionAsync(CancellationToken ct = default)
    {
        var response = await GetSessionUrlAsync(ct);

        if (response.Code != HttpStatusCode.OK || response.Content is null)
            throw new ChzzkApiException(response);

        return new ChzzkSession(new Uri(response.Content.Url));
    }

    #endregion Session
    #region User

    public Task<ChzzkResponse<UserInfo>> GetUserInfoAsync(CancellationToken ct = default)
        => SendUserAsync<UserInfo>(HttpMethod.Get, "/open/v1/users/me", ct: ct);

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
    #region Chat


    public Task<ChzzkResponse<ChatMessageContent>> PostChatMessageAsync(string message, CancellationToken ct = default)
        => SendUserAsync<ChatMessageContent>(HttpMethod.Post, "/open/v1/chats/send", body: new { message }, ct: ct);

    #endregion Chat
    #region Restriction
    #endregion Restriction
}

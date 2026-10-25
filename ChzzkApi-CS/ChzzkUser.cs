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

    readonly Lock refreshGate = new();
    Task<ChzzkResponse<TokenContent>>? refreshTask;

    public Task<ChzzkResponse<T>> SendUserAsync<T>(HttpMethod method, string path, Query? query = null, object? body = null, CancellationToken ct = default)
        => ClientApi.SendCoreAsync<ChzzkResponse<T>>(method, path, query, body, SetUserHeaders, ct);

    public Task<ChzzkResponse> SendUserAsync(HttpMethod method, string path, Query? query = null, object? body = null, CancellationToken ct = default)
        => ClientApi.SendCoreAsync<ChzzkResponse>(method, path, query, body, SetUserHeaders, ct);

    private void SetUserHeaders(HttpRequestHeaders headers)
        => headers.Authorization = new AuthenticationHeaderValue("Bearer", Token.AccessToken);


    #region Authorization

    public Task<ChzzkResponse<TokenContent>> RefreshAccessTokenAsync(CancellationToken ct = default)
    {
        lock (refreshGate)
            return refreshTask ??= RefreshCoreAsync(ct);
    }

    private async Task<ChzzkResponse<TokenContent>> RefreshCoreAsync(CancellationToken ct)
    {
        try
        {
            var response = await ClientApi.RefreshAccessTokenAsync(Token.RefreshToken, ct);

            if (response.Code == HttpStatusCode.OK)
            {
                Token = new ChzzkToken
                {
                    AccessToken = response.Content!.AccessToken,
                    RefreshToken = response.Content.RefreshToken
                };
            }

            return response;
        }
        finally
        {
            lock (refreshGate)
                refreshTask = null;
        }
    }

    public Task<ChzzkResponse> RevokeAccessTokenAsync(CancellationToken ct = default)
        => ClientApi.RevokeAccessTokenAsync(Token.AccessToken, ct: ct);

    #endregion Authorization
    #region Session

    public Task<ChzzkResponse<SessionListData>> GetSessionListAsync(int size = 20, string page = "0", CancellationToken ct = default)
        => SendUserAsync<SessionListData>(HttpMethod.Get, "/open/v1/sessions", [new("size", size.ToString()), new("page", page)], ct: ct);

    public async Task<ChzzkSession> CreateSessionAsync(CancellationToken ct = default)
    {
        var response = await SendUserAsync<SessionCreateContent>(HttpMethod.Get, "/open/v1/sessions/auth", ct: ct);

        if (response.Code != HttpStatusCode.OK)
            throw new ChzzkApiException(response);

        return new ChzzkSession(new Uri(response.Content!.Url));
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

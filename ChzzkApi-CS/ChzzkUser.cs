using ChzzkApi.Session;
using Microsoft.AspNetCore.WebUtilities;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace ChzzkApi;

public class ChzzkUser(ChzzkClient clientApi, string accessToken, string refreshToken)
{
    private ChzzkClient ClientApi { get; init; } = clientApi;

    public string AccessToken { get; private set; } = accessToken;
    public string RefreshToken { get; private set; } = refreshToken;

    readonly Lock refreshGate = new();
    Task<ChzzkResponse<TokenContent>>? refreshTask;

    private Task<T> SendUserCoreAsync<T>(HttpMethod method, string uri, HttpContent? content, CancellationToken ct = default)
        where T : ChzzkResponse
        => ClientApi.SendCoreAsync<T>(method, uri, content,
            headers => headers.Authorization = new AuthenticationHeaderValue("Bearer", AccessToken), ct);

    public Task<ChzzkResponse<T>> GetUserAsync<T>(string uri, CancellationToken ct = default)
        => SendUserCoreAsync<ChzzkResponse<T>>(HttpMethod.Get, uri, null, ct);

    public Task<ChzzkResponse> GetUserAsync(string uri, CancellationToken ct = default)
        => SendUserCoreAsync<ChzzkResponse>(HttpMethod.Get, uri, null, ct);

    public Task<ChzzkResponse<T>> PostUserAsync<T>(string uri, HttpContent? content, CancellationToken ct = default)
        => SendUserCoreAsync<ChzzkResponse<T>>(HttpMethod.Post, uri, content, ct);

    public Task<ChzzkResponse> PostUserAsync(string uri, HttpContent? content, CancellationToken ct = default)
        => SendUserCoreAsync<ChzzkResponse>(HttpMethod.Post, uri, content, ct);

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
            var response = await ClientApi.RefreshAccessTokenAsync(RefreshToken, ct);

            if (response.Code == HttpStatusCode.OK)
            {
                AccessToken = response.Content!.AccessToken;
                RefreshToken = response.Content.RefreshToken;
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
        => ClientApi.RevokeAccessTokenAsync(AccessToken, ct: ct);

    #endregion Authorization
    #region Session

    public Task<ChzzkResponse<SessionListData>> GetSessionListAsync(int size = 20, string page = "0", CancellationToken ct = default)
    {
        var uri = QueryHelpers.AddQueryString("/open/v1/sessions",
            new Dictionary<string, string?>
            {
                ["size"] = size.ToString(),
                ["page"] = page
            });

        return GetUserAsync<SessionListData>(uri, ct);
    }

    public async Task<ChzzkSession> CreateSessionAsync(CancellationToken ct = default)
    {
        var response = await GetUserAsync<SessionCreateContent>("/open/v1/sessions/auth", ct);

        if (response.Code != HttpStatusCode.OK)
            throw new ChzzkApiException(response);

        return new ChzzkSession(new Uri(response.Content!.Url));
    }

    #endregion Session
    #region User

    public Task<ChzzkResponse<UserInfo>> GetUserInfoAsync(CancellationToken ct = default)
        => GetUserAsync<UserInfo>("/open/v1/users/me", ct);

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
        => PostUserAsync<ChatMessageContent>("/open/v1/chats/send", JsonContent.Create(new { message }), ct);

    #endregion Chat
    #region Restriction
    #endregion Restriction
}

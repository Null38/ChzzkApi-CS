using ChzzkApi.Session;
using Microsoft.AspNetCore.WebUtilities;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace ChzzkApi;

public class ChzzkUserApi
{
    private ChzzkClientApi ClientApi { get; init; }

    public string AccessToken { get; private set; }
    public string RefreshToken { get; private set; }

    readonly Lock refreshGate = new();
    Task<ChzzkResponse<TokenContent>>? refreshTask;

    public ChzzkUserApi(ChzzkClientApi clientApi, string accessToken, string refreshToken)
    {
        ClientApi = clientApi;
        AccessToken = accessToken;
        RefreshToken = refreshToken;
    }

    private Task<T> SendUserCoreAsync<T>(HttpRequestMessage request, CancellationToken ct = default)
    {
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", AccessToken);

        return ClientApi.SendCoreAsync<T>(request, ct);
    }

    public Task<ChzzkResponse<T>> GetUserAsync<T>(string uri, CancellationToken ct = default)
        => SendUserCoreAsync<ChzzkResponse<T>>(new HttpRequestMessage(HttpMethod.Get, uri), ct);

    public Task<ChzzkResponse> GetUserAsync(string uri, CancellationToken ct = default)
        => SendUserCoreAsync<ChzzkResponse>(new HttpRequestMessage(HttpMethod.Get, uri), ct);

    public Task<ChzzkResponse<T>> PostUserAsync<T>(string uri, HttpContent? content, CancellationToken ct = default)
        => SendUserCoreAsync<ChzzkResponse<T>>(new HttpRequestMessage(HttpMethod.Post, uri) { Content = content }, ct);

    public Task<ChzzkResponse> PostUserAsync(string uri, HttpContent? content, CancellationToken ct = default)
        => SendUserCoreAsync<ChzzkResponse>(new HttpRequestMessage(HttpMethod.Post, uri) { Content = content }, ct);

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

            if (response.Code == ChzzkStatusCode.Success)
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

        if (response.Code != ChzzkStatusCode.Success)
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

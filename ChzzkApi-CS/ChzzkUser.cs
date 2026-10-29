using ChzzkApi.Session;
using ChzzkApi.Exceptions;
using ChzzkApi.Models;
using ChzzkApi.Rule;
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

    #region HTTP Requests

    public Task<ChzzkResponse<T>> SendUserAsync<T>(HttpMethod method, string path, Query? query = null, object? body = null, CancellationToken ct = default)
        => ClientApi.SendWithRulesAsync<ChzzkResponse<T>>(method, path, query, body, SetUserHeaders, this, ct);

    public Task<ChzzkResponse> SendUserAsync(HttpMethod method, string path, Query? query = null, object? body = null, CancellationToken ct = default)
        => ClientApi.SendWithRulesAsync<ChzzkResponse>(method, path, query, body, SetUserHeaders, this, ct);

    private void SetUserHeaders(HttpRequestHeaders headers)
        => headers.Authorization = new AuthenticationHeaderValue("Bearer", Token.AccessToken);

    #endregion HTTP Requests

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

    public Task<ChzzkResponse<ChzzkPage<SessionInfo>>> GetSessionsAsync(int? size = null, int page = 0, CancellationToken ct = default)
        => SendUserAsync<ChzzkPage<SessionInfo>>(HttpMethod.Get, "/open/v1/sessions", [new("size", size?.ToString()), new("page", page.ToString())], ct: ct);

    public Task<ChzzkResponse<SessionUrlContent>> GetSessionUrlAsync(CancellationToken ct = default)
        => SendUserAsync<SessionUrlContent>(HttpMethod.Get, "/open/v1/sessions/auth", ct: ct);

    public async Task<ChzzkSession> CreateSessionAsync(CancellationToken ct = default)
    {
        var response = await GetSessionUrlAsync(ct);

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

    public Task<ChzzkResponse<ChzzkPage<ManagerInfo>>> GetStreamingRolesAsync(CancellationToken ct = default)
        => SendUserAsync<ChzzkPage<ManagerInfo>>(HttpMethod.Get, "/open/v1/channels/streaming-roles", ct: ct);

    public Task<ChzzkResponse<ChzzkNumberedPage<FollowerInfo>>> GetFollowersAsync(int page = 0, int? size = null, CancellationToken ct = default)
        => SendUserAsync<ChzzkNumberedPage<FollowerInfo>>(HttpMethod.Get, "/open/v1/channels/followers", [new("page", page.ToString()), new("size", size?.ToString())], ct: ct);

    public Task<ChzzkResponse<ChzzkNumberedPage<SubscriberInfo>>> GetSubscribersAsync(int page = 0, int? size = null, SortOption? sort = null, CancellationToken ct = default)
        => SendUserAsync<ChzzkNumberedPage<SubscriberInfo>>(HttpMethod.Get, "/open/v1/channels/subscribers", [new("page", page.ToString()), new("size", size?.ToString()), new("sort", sort?.ToParamSegment())], ct: ct);

    #endregion Channel
    #region Live

    public Task<ChzzkResponse<StreamKeyContent>> GetStreamKeyAsync(CancellationToken ct = default)
        => SendUserAsync<StreamKeyContent>(HttpMethod.Get, "/open/v1/streams/key", ct: ct);

    public Task<ChzzkResponse<StreamSettings>> GetLiveSettingAsync(CancellationToken ct = default)
        => SendUserAsync<StreamSettings>(HttpMethod.Get, "/open/v1/lives/setting", ct: ct);

    public Task<ChzzkResponse> UpdateLiveSettingAsync(string? defaultLiveTitle = null, (CategoryType? type, string id)? category = null, string[]? tags = null, CancellationToken ct = default)
    {
        var body = new Dictionary<string, object>();

        if (defaultLiveTitle is not null) 
            body["defaultLiveTitle"] = defaultLiveTitle;
        if (category?.id is not null) 
            body["categoryId"] = category?.id!; 
        if (category?.type is not null) 
            body["categoryType"] = category?.type?.ToParamSegment()!; 
        if (tags is not null)
            body["tags"] = tags;

        return SendUserAsync(HttpMethod.Patch, "/open/v1/lives/setting", body: body, ct: ct);
    }

    #endregion Live
    #region Chat

    public Task<ChzzkResponse<ChatMessageContent>> PostChatMessageAsync(string message, CancellationToken ct = default)
        => SendUserAsync<ChatMessageContent>(HttpMethod.Post, "/open/v1/chats/send", body: new { message }, ct: ct);

    public Task<ChzzkResponse> PostChatNoticeAsync(string? message = null, string? messageId = null, CancellationToken ct = default)
        => SendUserAsync(HttpMethod.Post, "/open/v1/chats/notice", body: new { message, messageId }, ct: ct);

    public Task<ChzzkResponse<ChatSettings>> GetChatSettingsAsync(CancellationToken ct = default)
        => SendUserAsync<ChatSettings>(HttpMethod.Get, "/open/v1/chats/settings", ct: ct);

    public Task<ChzzkResponse<ChatSettings>> UpdateChatSettingsAsync(ChatAvailableConditionOption? chatAvailableCondition, ChatAvailableGroupOption? chatAvailableGroup,
        MinFollowerTimeOption? minFollowerTime, bool? allowSubscriberInFollowerMode, ChatSlowModeTimeOption? chatSlowModeTime, bool? chatEmojiMode,
        CancellationToken ct = default)
    {
        var body = new Dictionary<string, object>();

        if (chatAvailableCondition is not null)
            body["chatAvailableCondition"] = chatAvailableCondition;
        if (chatAvailableGroup is not null)
            body["chatAvailableGroup"] = chatAvailableGroup;
        if (minFollowerTime is not null)
            body["minFollowerMinute"] = (int)minFollowerTime;
        if (allowSubscriberInFollowerMode is not null)
            body["allowSubscriberInFollowerMode"] = allowSubscriberInFollowerMode;
        if (chatSlowModeTime is not null)
            body["chatSlowModeSec"] = (int)chatSlowModeTime;
        if (chatEmojiMode is not null)
            body["chatEmojiMode"] = chatEmojiMode;

        return SendUserAsync<ChatSettings>(HttpMethod.Put, "/open/v1/chats/settings", body: body, ct: ct);
    }

    public Task<ChzzkResponse> BlindChatMessageAsync(string chatChannelId, long messageTime, string senderChannelId, CancellationToken ct = default)
        => SendUserAsync(HttpMethod.Post, "/open/v1/chats/blind-message", body: new { chatChannelId, messageTime, senderChannelId }, ct: ct);

    #endregion Chat
    #region Restriction

    public Task<ChzzkResponse> AddRestrictionAsync(string targetChannelId, CancellationToken ct = default)
        => SendUserAsync(HttpMethod.Post, "/open/v1/restrict-channels", body: new { targetChannelId }, ct: ct);

    public Task<ChzzkResponse> RemoveRestrictionAsync(string targetChannelId, CancellationToken ct = default)
        => SendUserAsync(HttpMethod.Delete, "/open/v1/restrict-channels", body: new { targetChannelId }, ct: ct);

    public Task<ChzzkResponse<ChzzkCursorPage<RestrictedInfo>>> GetRestrictionsAsync(int? size = null, string? next = null, CancellationToken ct = default)
        => SendUserAsync<ChzzkCursorPage<RestrictedInfo>>(HttpMethod.Get, "/open/v1/restrict-channels", [new("size", size?.ToString()), new("next", next)], ct: ct);

    public Task<ChzzkResponse> AddTemporaryRestrictionAsync(string targetChannelId, string chatChannelId, CancellationToken ct = default)
        => SendUserAsync(HttpMethod.Post, "/open/v1/temporary-restrict-channels", body: new { targetChannelId, chatChannelId }, ct: ct);

    public Task<ChzzkResponse> RemoveTemporaryRestrictionAsync(string targetChannelId, string chatChannelId, CancellationToken ct = default)
        => SendUserAsync(HttpMethod.Delete, "/open/v1/temporary-restrict-channels", body: new { targetChannelId, chatChannelId }, ct: ct);

    #endregion Restriction
}

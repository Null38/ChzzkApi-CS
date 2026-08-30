using Microsoft.AspNetCore.WebUtilities;
using SocketIOClient;
using System.Text.Json;
using System.Web;

namespace ChzzkApi_CS.Session;

public class ChzzkSession : IAsyncDisposable
{
    readonly SocketIO socket;

    TaskCompletionSource<string> connectTcs = CreateConnectSource();

    public string SessionKey { get; private set; } = string.Empty;

    public event Action<EventData>? EventSubscribed;
    public event Action<EventData>? EventUnsubscribed;
    public event Action<EventData>? EventRevoked;
    public event Action<ChatMessage>? ChatReceived;
    public event Action<DonationMessage>? DonationReceived;
    public event Action<SubscriptionMessage>? SubscriptionReceived;

    internal ChzzkSession(Uri uri)
    {
        socket = new SocketIO(uri,
        new SocketIOOptions
        {
            EIO = SocketIOClient.Common.EngineIO.V3,
            Reconnection = false,
            ConnectionTimeout = TimeSpan.FromSeconds(3),
            Transport = SocketIOClient.Common.TransportProtocol.WebSocket,
            Query = HttpUtility.ParseQueryString(uri.Query)
        });

        socket.On("SYSTEM", OnSystem);
        socket.On("CHAT", OnChat);
        socket.On("DONATION", OnDonation);
        socket.On("SUBSCRIPTION", OnSubscription);
    }

    static TaskCompletionSource<string> CreateConnectSource()
        => new(TaskCreationOptions.RunContinuationsAsynchronously);

    public async Task ConnectAsync(CancellationToken ct = default)
    {
        var pending = connectTcs = CreateConnectSource();

        await socket.ConnectAsync(ct);

        using var registration = ct.Register(() => pending.TrySetCanceled(ct));

        SessionKey = await pending.Task;
    }

    public Task DisconnectAsync(CancellationToken ct = default) => socket.DisconnectAsync(ct);

    public Task<ChzzkResponse> SubscribeEventAsync(ChzzkUserApi userApi, EventType eventType, CancellationToken ct = default)
    {
        string uri = QueryHelpers.AddQueryString($"/open/v1/sessions/events/subscribe/{eventType.ToPathSegment()}", "sessionKey", SessionKey);

        return userApi.PostUserAsync(uri, null, ct);
    }

    public Task<ChzzkResponse> UnsubscribeEventAsync(ChzzkUserApi userApi, EventType eventType, CancellationToken ct = default)
    {
        string uri = QueryHelpers.AddQueryString($"/open/v1/sessions/events/unsubscribe/{eventType.ToPathSegment()}", "sessionKey", SessionKey);

        return userApi.PostUserAsync(uri, null, ct);
    }

    Task OnSystem(IEventContext response)
    {
        if (response.GetValue<JsonElement>(0).GetString() is not string payload)
            return Task.CompletedTask;

        var message = JsonSerializer.Deserialize<SystemMessage>(payload);
        if (message is null) return Task.CompletedTask;

        switch (message.Type)
        {
            case "connected":
                var key = message.Data.GetProperty("sessionKey").GetString()!;
                connectTcs.TrySetResult(key);
                break;
            case "subscribed":
                var subscribed = message.Data.Deserialize<EventData>();
                if (subscribed is not null) EventSubscribed?.Invoke(subscribed);
                break;
            case "unsubscribed":
                var unsubscribed = message.Data.Deserialize<EventData>();
                if (unsubscribed is not null) EventUnsubscribed?.Invoke(unsubscribed);
                break;
            case "revoked":
                var revoked = message.Data.Deserialize<EventData>();
                if (revoked is not null) EventRevoked?.Invoke(revoked);
                break;
        }
        return Task.CompletedTask;
    }

    Task OnChat(IEventContext response)
    {
        if (response.GetValue<JsonElement>(0).GetString() is not string payload)
            return Task.CompletedTask;

        var message = JsonSerializer.Deserialize<ChatMessage>(payload);
        if (message is not null)
            ChatReceived?.Invoke(message);
        return Task.CompletedTask;
    }

    Task OnDonation(IEventContext response)
    {
        if (response.GetValue<JsonElement>(0).GetString() is not string payload)
            return Task.CompletedTask;

        var message = JsonSerializer.Deserialize<DonationMessage>(payload);
        if (message is not null)
            DonationReceived?.Invoke(message);
        return Task.CompletedTask;
    }

    Task OnSubscription(IEventContext response)
    {
        if (response.GetValue<JsonElement>(0).GetString() is not string payload)
            return Task.CompletedTask;

        var message = JsonSerializer.Deserialize<SubscriptionMessage>(payload);
        if (message is not null)
            SubscriptionReceived?.Invoke(message);
        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        if (socket.Connected)
            await socket.DisconnectAsync();

        socket.Dispose();
    }
}

using Microsoft.AspNetCore.WebUtilities;
using SocketIOClient;
using System.Net.Http.Json;
using System.Text.Json;
using System.Web;

namespace ChzzkApi_CS.Session;

public partial class ChzzkSession : IAsyncDisposable
{
    readonly SocketIO socket;

    public string SessionKey { get; private set; } = string.Empty;

    readonly TaskCompletionSource<string> connectTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public event Action<EventData>? EventSubscribed;
    public event Action<EventData>? EventUnsubscribed;
    public event Action<EventData>? EventRevoked;
    public event Action<ChatMessage>? ChatReceived;
    public event Action<DonationMessage>? DonationReceived;
    public event Action<SubscriptionMessage>? SubscriptionReceived;

    public ChzzkSession(Uri uri)
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

    public async Task ConnectAsync()
    {
        await socket.ConnectAsync();
        SessionKey = await connectTcs.Task;
    }

    public async Task DisconnectAsync()
    {
        await socket.DisconnectAsync();
    }

    public Task<ChzzkResponse> SubscribeEventAsync(ChzzkUserApi api, EventType eventType)
    {
        string uri = QueryHelpers.AddQueryString($"/open/v1/sessions/events/subscribe/{ToPathSegment(eventType)}", "sessionKey", SessionKey);

        return api.PostUserAsync(uri, null);
    }

    public Task<ChzzkResponse> UnsubscribeEventAsync(ChzzkUserApi api, EventType eventType)
    {
        string uri = QueryHelpers.AddQueryString($"/open/v1/sessions/events/unsubscribe/{ToPathSegment(eventType)}", "sessionKey", SessionKey);

        return api.PostUserAsync(uri, null);
    }

    private static string ToPathSegment(EventType eventType) => eventType switch
    {
        EventType.Chat => "chat",
        EventType.Donation => "donation",
        EventType.Subscription => "subscription",
        _ => throw new ArgumentOutOfRangeException(nameof(eventType), eventType, null)
    };

    Task OnSystem(IEventContext response)
    {
        if (response.GetValue<JsonElement>(0).GetString() is not string payload)
            return Task.CompletedTask;

        var message = JsonSerializer.Deserialize<SystemMessage>(payload);
        if (message is null) return Task.CompletedTask;

        switch (message.Type)
        {
            case "connected":
                var key = message.Data.GetProperty("sessionKey").GetString();
                connectTcs.TrySetResult(key!);
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
        Console.WriteLine(response.RawText);

        if (response.GetValue<JsonElement>(0).GetString() is not string payload)
            return Task.CompletedTask;

        var message = JsonSerializer.Deserialize<ChatMessage>(payload);
        if (message is not null) ChatReceived?.Invoke(message);
        return Task.CompletedTask;
    }

    Task OnDonation(IEventContext response)
    {
        if (response.GetValue<JsonElement>(0).GetString() is not string payload)
            return Task.CompletedTask;

        var message = JsonSerializer.Deserialize<DonationMessage>(payload);
        if (message is not null) DonationReceived?.Invoke(message);
        return Task.CompletedTask;
    }

    Task OnSubscription(IEventContext response)
    {
        if (response.GetValue<JsonElement>(0).GetString() is not string payload)
            return Task.CompletedTask;

        var message = JsonSerializer.Deserialize<SubscriptionMessage>(payload);
        if (message is not null) SubscriptionReceived?.Invoke(message);
        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        if (socket.Connected)
            await socket.DisconnectAsync();
        socket.Dispose();
    }
}

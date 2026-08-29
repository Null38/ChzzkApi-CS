using Microsoft.AspNetCore.WebUtilities;
using SocketIOClient;
using System.Text.Json;
using System.Web;

namespace ChzzkApi_CS.Session;

public class ChzzkSession : IAsyncDisposable
{
    readonly SocketIO socket;
    readonly Func<string, CancellationToken, Task<ChzzkResponse>> postEventAsync;

    TaskCompletionSource<string> connectTcs = CreateConnectSource();

    public string SessionKey { get; private set; } = string.Empty;

    public event Func<EventData, Task>? EventSubscribed;
    public event Func<EventData, Task>? EventUnsubscribed;
    public event Func<EventData, Task>? EventRevoked;
    public event Func<ChatMessage, Task>? ChatReceived;
    public event Func<DonationMessage, Task>? DonationReceived;
    public event Func<SubscriptionMessage, Task>? SubscriptionReceived;

    /// <summary>
    /// Raised when a payload cannot be parsed or a handler throws. Socket callbacks have no
    /// caller to propagate to, so failures surface here instead of tearing down the process.
    /// </summary>
    public event Action<Exception>? MessageFailed;

    internal ChzzkSession(Uri uri, Func<string, CancellationToken, Task<ChzzkResponse>> postEventAsync)
    {
        this.postEventAsync = postEventAsync;

        socket = new SocketIO(uri,
        new SocketIOOptions
        {
            EIO = SocketIOClient.Common.EngineIO.V3,
            Reconnection = false,
            ConnectionTimeout = TimeSpan.FromSeconds(3),
            Transport = SocketIOClient.Common.TransportProtocol.WebSocket,
            Query = HttpUtility.ParseQueryString(uri.Query)
        });

        socket.OnDisconnected += OnDisconnected;

        socket.On("SYSTEM", OnSystem);
        socket.On("CHAT", OnChat);
        socket.On("DONATION", OnDonation);
        socket.On("SUBSCRIPTION", OnSubscription);
    }

    static TaskCompletionSource<string> CreateConnectSource()
        => new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>
    /// Connects and waits for the server to issue a session key.
    /// </summary>
    public async Task ConnectAsync(CancellationToken ct = default)
    {
        var pending = connectTcs = CreateConnectSource();

        await socket.ConnectAsync(ct).ConfigureAwait(false);

        using var registration = ct.Register(() => pending.TrySetCanceled(ct));

        SessionKey = await pending.Task.ConfigureAwait(false);
    }

    public Task DisconnectAsync(CancellationToken ct = default) => socket.DisconnectAsync(ct);

    public Task<ChzzkResponse> SubscribeEventAsync(EventType eventType, CancellationToken ct = default)
    {
        string uri = QueryHelpers.AddQueryString($"/open/v1/sessions/events/subscribe/{ToPathSegment(eventType)}", "sessionKey", SessionKey);

        return postEventAsync(uri, ct);
    }

    public Task<ChzzkResponse> UnsubscribeEventAsync(EventType eventType, CancellationToken ct = default)
    {
        string uri = QueryHelpers.AddQueryString($"/open/v1/sessions/events/unsubscribe/{ToPathSegment(eventType)}", "sessionKey", SessionKey);

        return postEventAsync(uri, ct);
    }

    private static string ToPathSegment(EventType eventType) => eventType switch
    {
        EventType.Chat => "chat",
        EventType.Donation => "donation",
        EventType.Subscription => "subscription",
        _ => throw new ArgumentOutOfRangeException(nameof(eventType), eventType, null)
    };

    void OnDisconnected(object? sender, string reason)
        => connectTcs.TrySetException(new ChzzkApiException(ChzzkStatusCode.InternalServerError, $"Session disconnected before a session key was issued: {reason}"));

    async Task OnSystem(IEventContext response)
    {
        if (Parse<SystemMessage>(response) is not SystemMessage message)
            return;

        switch (message.Type)
        {
            case "connected":
                if (message.Data.TryGetProperty("sessionKey", out var key) && key.GetString() is string sessionKey)
                    connectTcs.TrySetResult(sessionKey);
                break;
            case "subscribed":
                await InvokeAsync(EventSubscribed, ParseData(message.Data)).ConfigureAwait(false);
                break;
            case "unsubscribed":
                await InvokeAsync(EventUnsubscribed, ParseData(message.Data)).ConfigureAwait(false);
                break;
            case "revoked":
                await InvokeAsync(EventRevoked, ParseData(message.Data)).ConfigureAwait(false);
                break;
        }
    }

    Task OnChat(IEventContext response) => InvokeAsync(ChatReceived, Parse<ChatMessage>(response));

    Task OnDonation(IEventContext response) => InvokeAsync(DonationReceived, Parse<DonationMessage>(response));

    Task OnSubscription(IEventContext response) => InvokeAsync(SubscriptionReceived, Parse<SubscriptionMessage>(response));

    T? Parse<T>(IEventContext response)
    {
        try
        {
            if (response.GetValue<JsonElement>(0).GetString() is not string payload)
                return default;

            return JsonSerializer.Deserialize<T>(payload);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or IndexOutOfRangeException)
        {
            MessageFailed?.Invoke(ex);
            return default;
        }
    }

    EventData? ParseData(JsonElement data)
    {
        try
        {
            return data.Deserialize<EventData>();
        }
        catch (JsonException ex)
        {
            MessageFailed?.Invoke(ex);
            return null;
        }
    }

    async Task InvokeAsync<T>(Func<T, Task>? handlers, T? message)
    {
        if (handlers is null || message is null)
            return;

        foreach (var handler in handlers.GetInvocationList().Cast<Func<T, Task>>())
        {
            try
            {
                await handler(message).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                MessageFailed?.Invoke(ex);
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        socket.OnDisconnected -= OnDisconnected;

        if (socket.Connected)
            await socket.DisconnectAsync().ConfigureAwait(false);

        socket.Dispose();
    }
}

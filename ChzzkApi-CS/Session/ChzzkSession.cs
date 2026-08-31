using Microsoft.AspNetCore.WebUtilities;
using SocketIOClient;
using System.Text.Json;
using System.Web;

namespace ChzzkApi_CS.Session;

public class ChzzkSession : IAsyncDisposable
{
    // Also bounds the wait for the connected frame, which the transport timeout does not cover.
    static readonly TimeSpan ConnectionTimeout = TimeSpan.FromSeconds(3);

    readonly SocketIO socket;
    readonly TaskCompletionSource<string> connectTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

    int connectStarted;

    public string SessionKey { get; private set; } = string.Empty;

    /// <summary>
    /// Failures raised inside socket receive callbacks, which have no caller to throw to.
    /// Subscribe to <see cref="SessionErrorBuffer.ErrorOccurred"/> or drain it to observe them.
    /// </summary>
    public SessionErrorBuffer Errors { get; } = new();

    /// <remarks>
    /// Subscribers run synchronously on the socket receive thread, so a slow one delays every
    /// later message. Offload heavy or asynchronous work yourself and handle its failures there;
    /// an async lambda bound to this event compiles as async void and its failures are lost.
    /// </remarks>
    public event Action<EventData>? EventSubscribed;

    /// <inheritdoc cref="EventSubscribed"/>
    public event Action<EventData>? EventUnsubscribed;

    /// <inheritdoc cref="EventSubscribed"/>
    public event Action<EventData>? EventRevoked;

    /// <inheritdoc cref="EventSubscribed"/>
    public event Action<ChatMessage>? ChatReceived;

    /// <inheritdoc cref="EventSubscribed"/>
    public event Action<DonationMessage>? DonationReceived;

    /// <inheritdoc cref="EventSubscribed"/>
    public event Action<SubscriptionMessage>? SubscriptionReceived;

    internal ChzzkSession(Uri uri)
    {
        socket = new SocketIO(uri,
        new SocketIOOptions
        {
            EIO = SocketIOClient.Common.EngineIO.V3,
            Reconnection = false,
            ConnectionTimeout = ConnectionTimeout,
            Transport = SocketIOClient.Common.TransportProtocol.WebSocket,
            Query = HttpUtility.ParseQueryString(uri.Query)
        });

        socket.OnDisconnected += OnDisconnected;
        socket.OnError += OnError;

        socket.On("SYSTEM", OnSystem);
        socket.On("CHAT", response => Dispatch("CHAT", response, ChatReceived));
        socket.On("DONATION", response => Dispatch("DONATION", response, DonationReceived));
        socket.On("SUBSCRIPTION", response => Dispatch("SUBSCRIPTION", response, SubscriptionReceived));
    }

    /// <summary>
    /// Connects and waits for the server to issue a session key.
    /// </summary>
    /// <exception cref="InvalidOperationException">The session was already connected. A session URI is single-use.</exception>
    /// <exception cref="TimeoutException">The transport connected but no session key arrived.</exception>
    public async Task ConnectAsync(CancellationToken ct = default)
    {
        if (Interlocked.Exchange(ref connectStarted, 1) == 1)
            throw new InvalidOperationException("This session was already connected. Create a new session to connect again.");

        await socket.ConnectAsync(ct);

        SessionKey = await connectTcs.Task.WaitAsync(ConnectionTimeout, ct);
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

    void OnDisconnected(object? sender, string reason)
        => FailConnectOrReport(new IOException($"Session disconnected: {reason}"));

    void OnError(object? sender, string message)
        => FailConnectOrReport(new IOException($"Socket error: {message}"));

    Task OnSystem(IEventContext response)
    {
        try
        {
            if (response.GetValue<JsonElement>(0).GetString() is not string payload)
                return Task.CompletedTask;

            if (JsonSerializer.Deserialize<SystemMessage>(payload) is not SystemMessage message)
                return Task.CompletedTask;

            switch (message.Type)
            {
                case "connected":
                    CompleteConnect(message.Data);
                    break;
                case "subscribed":
                    Raise("SYSTEM", EventSubscribed, message.Data.Deserialize<EventData>());
                    break;
                case "unsubscribed":
                    Raise("SYSTEM", EventUnsubscribed, message.Data.Deserialize<EventData>());
                    break;
                case "revoked":
                    Raise("SYSTEM", EventRevoked, message.Data.Deserialize<EventData>());
                    break;
            }
        }
        catch (Exception ex)
        {
            FailConnectOrReport(ex, "SYSTEM");
        }

        return Task.CompletedTask;
    }

    void CompleteConnect(JsonElement data)
    {
        if (data.TryGetProperty("sessionKey", out var key) && key.GetString() is string sessionKey)
            connectTcs.TrySetResult(sessionKey);
        else
            connectTcs.TrySetException(new JsonException("The connected frame carried no sessionKey."));
    }

    Task Dispatch<T>(string source, IEventContext response, Action<T>? handler) where T : class
    {
        try
        {
            if (response.GetValue<JsonElement>(0).GetString() is not string payload)
                return Task.CompletedTask;

            Raise(source, handler, JsonSerializer.Deserialize<T>(payload));
        }
        catch (Exception ex)
        {
            Report(source, ex);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Invokes each subscriber in isolation so that one throwing does not skip the rest.
    /// </summary>
    void Raise<T>(string source, Action<T>? handler, T? message) where T : class
    {
        if (handler is null || message is null)
            return;

        foreach (var subscriber in handler.GetInvocationList().Cast<Action<T>>())
        {
            try
            {
                subscriber(message);
            }
            catch (Exception ex)
            {
                Report(source, ex);
            }
        }
    }

    /// <summary>
    /// Hands the failure to whoever is waiting on <see cref="ConnectAsync"/>, or buffers it once
    /// the connect has settled and no caller is left to throw to.
    /// </summary>
    void FailConnectOrReport(Exception exception, string source = "SOCKET")
    {
        if (!connectTcs.TrySetException(exception))
            Report(source, exception);
    }

    void Report(string source, Exception exception)
        => Errors.Add(new SessionError(DateTimeOffset.UtcNow, source, exception));

    public async ValueTask DisposeAsync()
    {
        socket.OnDisconnected -= OnDisconnected;
        socket.OnError -= OnError;

        if (socket.Connected)
            await socket.DisconnectAsync();

        socket.Dispose();
    }
}

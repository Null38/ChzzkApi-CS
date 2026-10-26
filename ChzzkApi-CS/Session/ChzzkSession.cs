using Microsoft.AspNetCore.WebUtilities;
using SocketIOClient;
using System.Text.Json;
using System.Web;

namespace ChzzkApi.Session;

public class ChzzkSession : IAsyncDisposable
{
    static readonly TimeSpan ConnectionTimeout = TimeSpan.FromSeconds(3);

    readonly SocketIO socket;
    readonly TaskCompletionSource<string> connectTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

    bool isConnectStarted = false;

    public string SessionKey { get; private set; } = string.Empty;

    public ExceptionBuffer Exceptions { get; } = [];

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

    public async Task ConnectAsync(CancellationToken ct = default)
    {
        if (Interlocked.Exchange(ref isConnectStarted, true) == true)
            throw new InvalidOperationException("This session was already connected. Create a new session to connect again.");

        await socket.ConnectAsync(ct);

        try
        {
            SessionKey = await connectTcs.Task.WaitAsync(ct);
        }
        catch (OperationCanceledException)
        {
            await DisposeAsync();
        }
    }

    public Task DisconnectAsync(CancellationToken ct = default) => socket.DisconnectAsync(ct);

    public Task<ChzzkResponse> SubscribeEventAsync(ChzzkUser userApi, EventType eventType, CancellationToken ct = default)
    {
        if (!socket.Connected)
            throw new InvalidOperationException("The session isn't connected.");

        string uri = QueryHelpers.AddQueryString($"/open/v1/sessions/events/subscribe/{eventType.ToPathSegment()}", "sessionKey", SessionKey);

        return userApi.SendUserAsync(HttpMethod.Post, uri, null, ct: ct);
    }

    public Task<ChzzkResponse> UnsubscribeEventAsync(ChzzkUser userApi, EventType eventType, CancellationToken ct = default)
    {
        if (!socket.Connected)
            throw new InvalidOperationException("The session isn't connected.");

        string uri = QueryHelpers.AddQueryString($"/open/v1/sessions/events/unsubscribe/{eventType.ToPathSegment()}", "sessionKey", SessionKey);

        return userApi.SendUserAsync(HttpMethod.Post, uri, null, ct: ct);
    }

    void OnDisconnected(object? sender, string reason)
        => connectTcs.TrySetException(new IOException($"Session disconnected before a session key was issued: {reason}"));

    void OnError(object? sender, string message)
    {
        IOException exception = new($"SocketIO error: {message}");

        if (!connectTcs.TrySetException(exception))
            Report("socketIO", exception);
    }

    Task OnSystem(IEventContext response)
    {
        if (Parse<SystemMessage>(response, "SYSTEM") is not SystemMessage message)
        {
            return Task.CompletedTask;
        }

        if (message.Type == "connected")
        {
            CompleteConnect(message.Data);
            return Task.CompletedTask;

        }

        if (Parse<EventData>(message.Data, "SYSTEM") is not EventData eventData)
        {
            return Task.CompletedTask;
        }

        switch (message.Type)
        {
            case "subscribed":
                Raise("[SYSTEM] subscribed", EventSubscribed, eventData);
                break;
            case "unsubscribed":
                Raise("[SYSTEM] unsubscribed", EventUnsubscribed, eventData);
                break;
            case "revoked":
                Raise("[SYSTEM] revoked", EventRevoked, eventData);
                break;
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
    T? Parse<T>(IEventContext response, string source)
    {
        try
        {
            return JsonSerializer.Deserialize<T>(response.GetValue<JsonElement>(0).GetString()!);
        }
        catch (Exception ex)
        {
            Report(source, ex);
            return default;
        }
    }
    T? Parse<T>(JsonElement element, string source)
    {
        try
        {
            return element.Deserialize<T>();
        }
        catch (Exception ex)
        {
            Report(source, ex);
            return default;
        }
    }

    Task Dispatch<T>(string source, IEventContext response, Action<T>? handler)
    {
        if (Parse<T>(response, source) is not T payload)
        {
            return Task.CompletedTask;
        }

        Raise(source, handler, payload);

        return Task.CompletedTask;
    }

    void Raise<T>(string source, Action<T>? handler, T? message)
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

    void Report(string source, Exception exception)
    {
        Exceptions.Add(new ExceptionInfo(DateTimeOffset.UtcNow, source, exception));
    }

    public async ValueTask DisposeAsync()
    {
        socket.OnDisconnected -= OnDisconnected;
        socket.OnError -= OnError;

        if (socket.Connected)
            await socket.DisconnectAsync();
        
        socket.Dispose();
    }
}

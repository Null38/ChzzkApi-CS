using SocketIOClient;
using System.Text.Json;
using System.Threading.Channels;
using System.Web;

namespace ChzzkApi.Session;

public class ChzzkSession : IAsyncDisposable
{
    static readonly TimeSpan ConnectionTimeout = TimeSpan.FromSeconds(3);

    readonly SocketIO socket;
    readonly TaskCompletionSource<string> connectTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

    readonly Channel<Func<Task>> queue = Channel.CreateUnbounded<Func<Task>>(new UnboundedChannelOptions { SingleReader = true });

    public bool IsConnected => socket.Connected;

    bool isConnectStarted = false;
    volatile bool isClosing = false;

    readonly Lock closeGate = new();
    Task? closeTask;

    public string SessionKey { get; private set; } = string.Empty;

    public ExceptionBuffer Exceptions { get; } = [];

    public event Func<string, Task>? Disconnected;
    public event Func<(SystemMessageType MessageType, EventData Data), Task>? EventSubscriptionChanged;
    public event Func<ChatMessage, Task>? ChatReceived;
    public event Func<DonationMessage, Task>? DonationReceived;
    public event Func<SubscriptionMessage, Task>? SubscriptionReceived;

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
        socket.On("CHAT", response => Dispatch("[Chat]", response, ChatReceived));
        socket.On("DONATION", response => Dispatch("[Donation]", response, DonationReceived));
        socket.On("SUBSCRIPTION", response => Dispatch("[Subscription]", response, SubscriptionReceived));

        _ = RunQueueAsync();
    }

    public async Task ConnectAsync(CancellationToken ct = default)
    {
        if (Interlocked.Exchange(ref isConnectStarted, true) == true)
            throw new InvalidOperationException("This session was already connected. Create a new session to connect again.");

        try
        {
            await socket.ConnectAsync(ct);
            SessionKey = await connectTcs.Task.WaitAsync(ConnectionTimeout, ct);
        }
        catch
        {
            await DisconnectAsync(CancellationToken.None);
            throw;
        }
    }

    public Task DisconnectAsync(CancellationToken ct = default)
    {
        lock (closeGate)
            return (closeTask ??= CloseCoreAsync()).WaitAsync(ct);
    }

    async Task CloseCoreAsync()
    {
        isClosing = true;
        connectTcs.TrySetException(new ObjectDisposedException(nameof(ChzzkSession)));

        if (socket.Connected)
            await socket.DisconnectAsync();

        socket.OnDisconnected -= OnDisconnected;
        socket.OnError -= OnError;

        socket.Dispose();

        queue.Writer.TryComplete();
    }

    public Task<ChzzkResponse> SubscribeEventAsync(ChzzkUser userApi, EventType eventType, CancellationToken ct = default)
    {
        if (!socket.Connected)
            throw new InvalidOperationException("The session isn't connected.");

        return userApi.SendUserAsync(HttpMethod.Post, $"/open/v1/sessions/events/subscribe/{eventType.ToPathSegment()}", query: [new ("sessionKey", SessionKey)], ct: ct);
    }

    public Task<ChzzkResponse> UnsubscribeEventAsync(ChzzkUser userApi, EventType eventType, CancellationToken ct = default)
    {
        if (!socket.Connected)
            throw new InvalidOperationException("The session isn't connected.");

        return userApi.SendUserAsync(HttpMethod.Post, $"/open/v1/sessions/events/unsubscribe/{eventType.ToPathSegment()}", query: [new("sessionKey", SessionKey)], ct: ct);
    }

    void OnDisconnected(object? sender, string reason)
    {
        if (connectTcs.TrySetException(new IOException($"Session disconnected before a session key was issued: {reason}")))
            return;

        if (isClosing)
            return;

        Raise("disconnected", Disconnected, reason);
    }

    void OnError(object? sender, string message)
    {
        IOException exception = new($"SocketIO error: {message}");

        if (!connectTcs.TrySetException(exception))
            Report("socketIO", exception);
    }

    const string SystemSource = "[System]";

    Task OnSystem(IEventContext response)
    {
        if (Parse<SystemMessage>(response, SystemSource) is not SystemMessage message)
            return Task.CompletedTask;

        switch (message.Type)
        {
            case SystemMessageType.Connected:
                if (message.Data.SessionKey is null)
                {
                    connectTcs.TrySetException(new JsonException($"The connected frame carried no sessionKey:\n{response.RawText}"));
                    break;
                }

                connectTcs.TrySetResult(message.Data.SessionKey);
                break;
            case SystemMessageType.Subscribed:
            case SystemMessageType.Unsubscribed:
            case SystemMessageType.Revoked:
                if (message.Data.EventType is null)
                {
                    Report(SystemSource, new JsonException($"The {message.Type} frame carried no EventType:\n{response.RawText}"));
                    break;
                }

                if (message.Data.ChannelId is null)
                {
                    Report(SystemSource, new JsonException($"The {message.Type} frame carried no ChannelId:\n{response.RawText}"));
                    break;
                }


                Raise(SystemSource, EventSubscriptionChanged, (message.Type, new EventData(message.Data.EventType.Value, message.Data.ChannelId)));
                break;
        }

        return Task.CompletedTask;
    }

    T? Parse<T>(IEventContext response, string source)
    {
        try
        {
            var payload = JsonSerializer.Deserialize<T>(response.GetValue<string>(0) ?? string.Empty);

            if (payload is null)
                Report(source, new JsonException($"The {typeof(T).Name} frame deserialized to null:\n{response.RawText}"));

            return payload;
        }
        catch (Exception ex)
        {
            Report(source, ex);
            return default;
        }
    }

    Task Dispatch<T>(string source, IEventContext response, Func<T, Task>? handler)
    {
        if (Parse<T>(response, source) is not T payload)
            return Task.CompletedTask;

        Raise(source, handler, payload);

        return Task.CompletedTask;
    }

    void Raise<T>(string source, Func<T, Task>? handler, T message)
    {
        if (handler is null || message is null)
            return;

        queue.Writer.TryWrite(async () =>
        {
            foreach (var subscriber in handler.GetInvocationList().Cast<Func<T, Task>>())
            {
                try
                {
                    await subscriber(message);
                }
                catch (Exception ex)
                {
                    Report(source, ex);
                }
            }
        });
    }

    async Task RunQueueAsync()
    {
        await foreach (var work in queue.Reader.ReadAllAsync())
            await work();
    }

    void Report(string source, Exception exception)
    {
        Exceptions.Add(new ExceptionInfo(DateTimeOffset.UtcNow, source, exception));
    }

    async ValueTask IAsyncDisposable.DisposeAsync()
    {
        await DisconnectAsync();
        GC.SuppressFinalize(this);
    }
}

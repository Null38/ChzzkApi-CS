using ChzzkApi;
using ChzzkApi.Session;
using ChzzkApi.Extensions;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Example;

record AppConfig(
    [property: JsonPropertyName("clientId")] string ClientId,
    [property: JsonPropertyName("clientSecret")] string ClientSecret,
    [property: JsonPropertyName("accessToken")] string? AccessToken = null,
    [property: JsonPropertyName("refreshToken")] string? RefreshToken = null);

internal static class Program
{
    const string ConfigFile = "config.json";
    const string RedirectUri = "http://localhost:8080/api/path/";

    static IHttpClientFactory httpClientFactory;
    static ChzzkClient clientApi;
    static ChzzkUser? userApi;
    static AppConfig config;
    static readonly HashSet<string> pendingEchoes = [];

    static Program()
    {
        config = TryLoadJson<AppConfig>(ConfigFile)
            ?? CreateDefaultConfig(ConfigFile);

        var services = new ServiceCollection();
        services.AddChzzkApiClient();

        var sp = services.BuildServiceProvider();
        httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();

        clientApi = new ChzzkClient(
            httpClientFactory,
            config.ClientId,
            config.ClientSecret);

        if (config.AccessToken != null)
            userApi = new ChzzkUser(clientApi, config.AccessToken, config.RefreshToken ?? string.Empty);
    }

    static async Task Main(string[] args)
    {
        if (userApi is null)
            await RunOAuthFlowAsync();

        Console.WriteLine($"Access Token: {userApi!.AccessToken}");

        await CallWithAuthAsync(() => userApi.GetUserInfoAsync());

        var session = await userApi.CreateSessionAsync();
        await session.ConnectAsync();

        var subscribeRes = await CallWithAuthAsync(() => session.SubscribeEventAsync(userApi, EventType.Chat));
        Console.WriteLine($"Subscribe: {subscribeRes.Code} {subscribeRes.Message}");

        session.Exceptions.SetExceptionOccurred(error
            => Console.WriteLine($"[{error.Exception.Data[ExceptionInfo.HandlingContextDataKey]}] {error.Exception.GetType().Name}: {error.Exception.Message}"));

        // Handlers run on the receive thread, so the echo is offloaded rather than awaited here.
        // An async lambda would compile as async void and lose the send failure.
        session.ChatReceived += (msg) =>
        {
            Console.WriteLine($"{msg.Content}");

            if (msg.ChannelId == msg.SenderChannelId && pendingEchoes.Remove(msg.Content))
                return;

            pendingEchoes.Add(msg.Content);

            _ = Task.Run(async () =>
            {
                try
                {
                    await userApi.PostChatMessageAsync(msg.Content);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Echo failed: {ex.Message}");
                }
            });
        };

        var sessions = await userApi.GetSessionListAsync();
        if (sessions.Code == ChzzkStatusCode.Success)
        {
            Console.WriteLine("=== User Sessions ===");
            foreach (var s in sessions.Content!.Data)
            {
                Console.WriteLine($"Key: {s.SessionKey,14}");
                Console.WriteLine($"Connected: {s.ConnectedDate,14}");
                Console.WriteLine($"Disconnected: {s.DisconnectedDate,14}");
                foreach (var ev in s.SubscribedEvents)
                    Console.WriteLine($"   Event: {ev.EventType} / Channel: {ev.ChannelId}");
            }
        }

        Console.WriteLine("Press 'q' to quit.");
        while (Console.ReadLine()?.Equals("q", StringComparison.OrdinalIgnoreCase) != true) { }

        await CallWithAuthAsync(() => session.UnsubscribeEventAsync(userApi, EventType.Chat));
        await session.DisposeAsync();
    }

    static void UpdateTokens()
    {
        if (userApi != null)
            return;

        config = config with
        {
            AccessToken = userApi!.AccessToken,
            RefreshToken = userApi.RefreshToken
        };
        SaveJson(ConfigFile, config);
    }

    static async Task RunOAuthFlowAsync()
    {
        var authUri = clientApi.GetAuthorizationUri(RedirectUri, out string state);
        OpenUrl(authUri);
        var code = await ChzzkClient.WaitForAuthorizationCodeAsync(RedirectUri, state);

        userApi = await clientApi.IssueAccessTokenAsync(code, state);
        UpdateTokens();
    }

    static async Task<T> CallWithAuthAsync<T>(Func<Task<T>> apiCall)
        where T : ChzzkResponse
    {
        var response = await apiCall();

        if (response.Code == ChzzkStatusCode.Success)
            return response;
        if (response.Code != ChzzkStatusCode.Unauthorized)
            throw new ChzzkApiException(response);

        var refreshed = await userApi!.RefreshAccessTokenAsync();

        if (refreshed.Code == ChzzkStatusCode.Success)
        {
            UpdateTokens();
            return await apiCall();
        }
        if (refreshed.Code != ChzzkStatusCode.Unauthorized)
            throw new ChzzkApiException(refreshed);

        await RunOAuthFlowAsync();
        return await apiCall();
    }

    static AppConfig CreateDefaultConfig(string path)
    {
        var defaultConfig = new AppConfig("YOUR_CLIENT_ID", "YOUR_CLIENT_SECRET");
        SaveJson(path, defaultConfig);
        throw new InvalidOperationException($"'{path}' created. Fill in clientId and clientSecret.");
    }

    static T? TryLoadJson<T>(string path)
    {
        if (!File.Exists(path)) return default;
        using var stream = File.OpenRead(path);
        return JsonSerializer.Deserialize<T>(stream);
    }

    static void SaveJson<T>(string path, T value)
    {
        using var stream = File.Create(path);
        JsonSerializer.Serialize(stream, value, new JsonSerializerOptions { WriteIndented = true });
    }

    static void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
        }
        catch
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                Process.Start(new ProcessStartInfo("cmd", $"/c start {url.Replace("&", "^&")}") { CreateNoWindow = true });
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                Process.Start("xdg-open", url);
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                Process.Start("open", url);
            else
                throw;
        }
    }
}

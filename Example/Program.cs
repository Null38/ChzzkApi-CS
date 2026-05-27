using ChzzkApi_CS;
using ChzzkApi_CS.Session;
using ChzzkApi_CS.Extensions;
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

    static ChzzkApi api;
    static AppConfig config;

    static Program()
    {

        config = TryLoadJson<AppConfig>(ConfigFile)
            ?? CreateDefaultConfig(ConfigFile);

        var services = new ServiceCollection();
        services.AddChzzkApiClient();

        api = services.BuildServiceProvider()
            .GetRequiredService<ChzzkApi.Factory>()
            .Create(config.ClientId, config.ClientSecret);
    }

    static async Task Main(string[] args)
    {
        if (config.AccessToken is null)
            await RunOAuthFlowAsync();

        Console.WriteLine($"Access Token: {config.AccessToken}");

        var session = await api.CreateClientAsync();
        await session.ConnectAsync();

        var subscribeRes = await CallWithAuthAsync(() => session.SubscribeEventAsync(config.AccessToken!, EventType.Chat));

        Console.WriteLine($"Subscribe: {subscribeRes.Code} {subscribeRes.Message}");

        session.ChatReceived += (msg) => Console.WriteLine(msg.Content);

        var sessions = await api.GetClientSessionList();
        Console.WriteLine("=== Client Sessions ===");
        foreach (var s in sessions)
        {
            Console.WriteLine($"Key: {s.SessionKey,14}");
            Console.WriteLine($"Connected: {s.ConnectedDate,14}");
            Console.WriteLine($"Disconnected: {s.DisconnectedDate,14}");
            foreach (var ev in s.SubscribedEvents)
                Console.WriteLine($"Event: {ev.EventType} / Channel: {ev.ChannelId}");
        }

        Console.WriteLine("Press 'q' to quit.");
        while (Console.ReadLine()?.Equals("q", StringComparison.OrdinalIgnoreCase) != true) { }

        await session.UnsubscribeEventAsync(config.AccessToken, EventType.Chat);
        await session.DisposeAsync();
    }

    static void UpdateTokens(TokenContent tokens)
    {
        config = config with
        {
            AccessToken = tokens.AccessToken,
            RefreshToken = tokens.RefreshToken
        };
        SaveJson(ConfigFile, config);
    }

    static async Task RunOAuthFlowAsync()
    {
        var authUri = api.GetAuthorizationUri(RedirectUri, out string state);
        OpenUrl(authUri);
        var code = await ChzzkApi.WaitForAuthorizationCodeAsync(RedirectUri, state);

        var issued = await api.IssueAccessTokenAsync(code, state);
        if (issued.Code != ChzzkApiException.StatusCode.Success)
            throw new ChzzkApiException(issued);

        UpdateTokens(issued.Content!);
        return;
    }

    static async Task<T> CallWithAuthAsync<T>(Func<Task<T>> apiCall)
        where T : ChzzkResponse
    {
        var response = await apiCall();

        if (response.Code == ChzzkApiException.StatusCode.Success)
            return response;
        if (response.Code != ChzzkApiException.StatusCode.Unauthorized)
            throw new ChzzkApiException(response);


        var refreshed = await api.RefreshAccessTokenAsync(config.RefreshToken!);

        if (refreshed.Code == ChzzkApiException.StatusCode.Success)
        {
            UpdateTokens(refreshed.Content!);
            return await apiCall();
        }
        if (refreshed.Code != ChzzkApiException.StatusCode.Unauthorized)
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
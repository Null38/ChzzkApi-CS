using ChzzkApi_CS;
using ChzzkApi_CS.Session;
using ChzzkApi_CS.Extensions;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Example;

internal static class Program
{
    const string ConfigFile = "config.json";
    const string RedirectUri = "http://localhost:8080/api/path/";

    static async Task Main(string[] args)
    {
        var config = TryLoadJson<AppConfig>(ConfigFile)
            ?? CreateDefaultConfig(ConfigFile);

        var services = new ServiceCollection();
        services.AddChzzkApiClient();

        var api = services.BuildServiceProvider()
            .GetRequiredService<ChzzkApi.Factory>()
            .Create(config.ClientId, config.ClientSecret);

        await RunAsync(api, config);
    }

    static async Task RunAsync(ChzzkApi api, AppConfig config)
    {
        if (config.AccessToken is null)
            config = await RunOAuthFlowAsync(api, config);

        Console.WriteLine($"Access Token: {config.AccessToken}");

        var session = await api.CreateClientAsync();
        await session.ConnectAsync();

        var subscribeRes = await session.SubscribeEventAsync(config.AccessToken!, EventType.Chat);

        if (subscribeRes.Code == ChzzkApiException.StatusCode.Unauthorized)
        {
            if (config.RefreshToken is not null)
            {
                var refreshed = await api.RefreshAccessTokenAsync(config.RefreshToken);

                if (refreshed.Code == ChzzkApiException.StatusCode.Unauthorized)
                    config = await RunOAuthFlowAsync(api, config);
                else if (refreshed.Code != ChzzkApiException.StatusCode.Success)
                    throw new ChzzkApiException(refreshed);
                else
                {
                    config = config with
                    {
                        AccessToken = refreshed.Content!.AccessToken,
                        RefreshToken = refreshed.Content!.RefreshToken
                    };
                    SaveJson(ConfigFile, config);
                }
            }
            else
            {
                config = await RunOAuthFlowAsync(api, config);
            }

            subscribeRes = await session.SubscribeEventAsync(config.AccessToken!, EventType.Chat);
        }

        if (subscribeRes.Code != ChzzkApiException.StatusCode.Success)
            throw new ChzzkApiException(subscribeRes);

        Console.WriteLine($"Subscribe: {subscribeRes.Code} {subscribeRes.Message}");

        var sessions = await api.GetClientSessionList();
        Console.WriteLine("=== Client Sessions ===");
        foreach (var s in sessions)
        {
            Console.WriteLine($"  Key:          {s.SessionKey}");
            Console.WriteLine($"  Connected:    {s.ConnectedDate}");
            Console.WriteLine($"  Disconnected: {s.DisconnectedDate}");
            foreach (var ev in s.SubscribedEvents)
                Console.WriteLine($"    Event: {ev.EventType} / Channel: {ev.ChannelId}");
        }

        Console.WriteLine("Press 'q' to quit.");
        while (Console.ReadLine()?.Equals("q", StringComparison.OrdinalIgnoreCase) != true) { }

        await session.UnsubscribeEventAsync(config.AccessToken, EventType.Chat);
        await api.RevokeAccessTokenAsync(config.AccessToken);
        await session.DisposeAsync();
    }

    static async Task<AppConfig> RunOAuthFlowAsync(ChzzkApi api, AppConfig config)
    {
        var authUri = api.GetAuthorizationUri(RedirectUri, out string state);
        OpenUrl(authUri);
        var code = await ChzzkApi.WaitForAuthorizationCodeAsync(RedirectUri, state);

        var issued = await api.IssueAccessTokenAsync(code, state);
        if (issued.Code != ChzzkApiException.StatusCode.Success)
            throw new ChzzkApiException(issued);

        var updated = config with
        {
            AccessToken = issued.Content!.AccessToken,
            RefreshToken = issued.Content!.RefreshToken
        };
        SaveJson(ConfigFile, updated);
        return updated;
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

    record AppConfig(
        [property: JsonPropertyName("clientId")] string ClientId,
        [property: JsonPropertyName("clientSecret")] string ClientSecret,
        [property: JsonPropertyName("accessToken")] string? AccessToken = null,
        [property: JsonPropertyName("refreshToken")] string? RefreshToken = null);
}
using ChzzkApi;
using ChzzkApi.Extensions;
using ChzzkApi.Exceptions;
using ChzzkApi.Models;
using ChzzkApi.Rule;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;
using System.Net;
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

        clientApi.Rules.Add(null, OnResponseAsync);

        if (config.AccessToken != null)
            userApi = new ChzzkUser(clientApi, new ChzzkUser.ChzzkToken
            {
                AccessToken = config.AccessToken,
                RefreshToken = config.RefreshToken ?? string.Empty
            });
    }

    static async Task Main(string[] args)
    {
        if (userApi is null)
            await RunOAuthFlowAsync();

        var session = await userApi!.CreateSessionAsync();
        session.ChatReceived += chat =>
        {
            Console.WriteLine($"[CHAT] {chat.Profile.Nickname}: {chat.Content}");
            return Task.CompletedTask;
        };

        await session.ConnectAsync();
        Console.WriteLine($"Connected: {session.SessionKey}");

        var subscribed = await session.SubscribeEventAsync(userApi, EventType.Chat);
        Console.WriteLine($"Subscribe CHAT: {(int)subscribed.Code} {subscribed.Message}");

        Console.WriteLine("Press Enter to disconnect.");
        Console.ReadLine();

        await session.DisconnectAsync();

        foreach (var info in session.Exceptions)
            Console.WriteLine($"[EXCEPTION] {info.TimeUtc:O} {info.Exception}");
    }

    static async Task OnResponseAsync(ChzzkRuleContext ctx, CancellationToken ct)
    {
        switch (ctx.Response.Code)
        {
            case HttpStatusCode.OK:
                break;
            case HttpStatusCode.Unauthorized when ctx.User is not null:
                if (ReferenceEquals(ctx.SentToken, ctx.User.Token))
                {
                    var refreshed = await ctx.User.RefreshAccessTokenAsync(ct);
                    if (refreshed.Code != HttpStatusCode.OK)
                        return;

                    UpdateTokens();
                }

                await ctx.ResendAsync(ct);
                break;
            default:
                throw new ChzzkApiException(ctx.Response);
        }
    }
    static void UpdateTokens()
    {
        if (userApi is null)
            return;

        config = config with
        {
            AccessToken = userApi!.Token.AccessToken,
            RefreshToken = userApi.Token.RefreshToken
        };
        SaveJson(ConfigFile, config);
    }

    static async Task RunOAuthFlowAsync()
    {
        var authUri = clientApi.BuildAuthUrl(RedirectUri, out string state);
        var codeTask = ChzzkClient.WaitForAuthCodeAsync(RedirectUri, state);
        OpenUrl(authUri);

        userApi = await clientApi.CreateUserAsync(await codeTask, state);
        UpdateTokens();
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

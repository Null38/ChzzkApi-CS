using ChzzkApi;
using ChzzkApi.Exceptions;
using ChzzkApi.Extensions;
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
    const string RedirectUri = "http://localhost:8080/api/path/";
    private static readonly string savePath = Path.Combine(AppContext.BaseDirectory, ".secret");

    static readonly ServiceProvider serviceProvider;
    static AppConfig config;

    static Program()
    {
        if (!File.Exists(savePath))
            CreateDefaultConfig(savePath);

        using (var stream = File.OpenRead(savePath))
        {
            config = JsonSerializer.Deserialize<AppConfig>(stream)
                ?? throw new InvalidDataException($"'{savePath}' does not contain a valid config.");
        }


        var services = new ServiceCollection();
        services.AddChzzkApiClient();

        serviceProvider = services.BuildServiceProvider();
    }

    static async Task Main()
    {
        var client = new ChzzkClient(
            serviceProvider.GetRequiredService<IHttpClientFactory>(),
            config.ClientId,
            config.ClientSecret);

        client.Rules.Add("TokenRefresh", TokenRefreshRule);

        ChzzkUser user;

        if (config.AccessToken is null || config.RefreshToken is null)
            user = await RunOAuthFlowAsync(client);
        else
            user = new(client, new() { AccessToken = config.AccessToken, RefreshToken = config.RefreshToken });

        config = config with
        {
            AccessToken = user.Token.AccessToken,
            RefreshToken = user.Token.RefreshToken
        };

        Console.WriteLine((await client.SearchCategoriesAsync("셀레스")).Content);

        var session = await user.CreateSessionAsync();

        session.Exceptions.SetExceptionOccurred(ex => Console.WriteLine(((ChzzkApiException)ex.Exception).Response.Raw));

        session.ChatReceived += chat =>
        {
            Console.WriteLine($"[CHAT] {chat.Profile.Nickname}: {chat.Content}");
            return Task.CompletedTask;
        };

        await session.ConnectAsync();
        await session.SubscribeEventAsync(user, EventType.Chat);

        Console.WriteLine($"Connected {session.IsConnected}");

        Console.WriteLine("Press Enter to disconnect.");
        Console.ReadLine();


        await session.DisconnectAsync();

        SaveJson(savePath, config);
    }

    static async Task TokenRefreshRule(ChzzkRuleContext ctx, CancellationToken ct) // 이게 작동하는지 테스트 못해봄. 이건 시간이 지나야 테스트 가능..
    {
        switch (ctx.Response.Code)
        {
            case HttpStatusCode.OK:
                break;
            case HttpStatusCode.Unauthorized when ctx.User is not null:
                if (ChzzkApiException.GetAuthError(ctx.Response) == ChzzkAuthError.InvalidClient)
                    throw new ChzzkApiException(ctx.Response);

                if (ReferenceEquals(ctx.SentToken, ctx.User.Token))
                {
                    var refreshed = await ctx.User.RefreshAccessTokenAsync(ct);
                    if (ChzzkApiException.GetAuthError(refreshed) != ChzzkAuthError.InvalidToken)
                        throw new ChzzkApiException(refreshed);

                    var authUri = ctx.Client.BuildAuthUrl(RedirectUri, out string state);
                    var codeTask = ChzzkClient.WaitForAuthCodeAsync(RedirectUri, state, ct: CancellationToken.None);

                    OpenUrl(authUri);

                    refreshed = await ctx.User.IssueTokenAsync(await codeTask, state, ct);

                    if (refreshed.Code != HttpStatusCode.OK)
                        throw new ChzzkApiException(refreshed);
                }

                config = config with
                {
                    AccessToken = ctx.User.Token.AccessToken,
                    RefreshToken = ctx.User.Token.RefreshToken
                };

                await ctx.ResendAsync(ct);
                break;
            default:
                throw new ChzzkApiException(ctx.Response);
        }
    }

    static async Task<ChzzkUser> RunOAuthFlowAsync(ChzzkClient client)
    {
        var authUri = client.BuildAuthUrl(RedirectUri, out string state);
        var codeTask = ChzzkClient.WaitForAuthCodeAsync(RedirectUri, state);

        OpenUrl(authUri);

        return await client.CreateUserAsync(await codeTask, state);
    }

    private static void CreateDefaultConfig(string path)
    {
        var defaultConfig = new AppConfig("YOUR_CLIENT_ID", "YOUR_CLIENT_SECRET");

        SaveJson(path, defaultConfig);
        throw new InvalidOperationException($"'{savePath}' created. Fill in clientId and clientSecret.");
    }

    private static readonly JsonSerializerOptions jsonSerializerOptions = new() { WriteIndented = true };

    static void SaveJson<T>(string path, T value)
    {
        using var stream = File.Create(path);
        JsonSerializer.Serialize(stream, value, jsonSerializerOptions);
    }

    static bool OpenUrl(string url)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            Process.Start(new ProcessStartInfo("cmd", $"/c start {url.Replace("&", "^&")}") { CreateNoWindow = true });
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            Process.Start("xdg-open", url);
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            Process.Start("open", url);
        else
            return false;

        return true;
    }
}

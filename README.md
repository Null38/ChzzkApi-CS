# ChzzkApi-CS
[![.NET](https://img.shields.io/badge/.NET-9.0-512BD4)](https://dotnet.microsoft.com/download/dotnet/9.0)
[![NuGet](https://img.shields.io/nuget/v/ChzzkApi-CS)](https://www.nuget.org/packages/ChzzkApi-CS) 
[![License](https://img.shields.io/github/license/Null38/ChzzkApi-CS)](https://github.com/Null38/ChzzkApi-CS/blob/main/LICENSE.md)


네이버 라이브 스트리밍 서비스 [CHZZK](https://chzzk.naver.com/)의 공식 [Open API](https://chzzk.gitbook.io/chzzk)를 C#으로 감싼 비공식 라이브러리입니다.

공식 문서와 실제 반환의 차이는 [이 문서](https://github.com/Null38/ChzzkApi-CS/blob/main/docs/공식%20문서와%20차이점.md)에서 확인할 수 있습니다.

## 설치

```
dotnet add package ChzzkApi-CS
```

## 예시

> [!WARNING]
> **실행 파일에 API Key를 상수로 넣지 않는 걸 권합니다.**
>
> 공개하면 안 될 API Key는 Client Secret, Access Token, Refresh Token입니다. 
> - Client Secret은 해당 애플리케이션이 가진 권한의 API를 호출할 수 있습니다.
> - Access Token, Refresh Token은 허용된 권한에 따라 해당 채널의 정보를 취하거나 제어할 수 있습니다.
> 
> 이 라이브러리는 최종 사용자가 이 라이브러리로 만든 프로그램을 실행하는 것을 상정하고 만들어졌습니다. 만약 프로그램에 Key를 포함하고 배포한다면, 디컴파일로 Key를 꺼내는 게 가능해집니다.

API Key는 [치지직 개발자 센터](https://developers.chzzk.naver.com/)에서 발급할 수 있습니다.

### 연결

```C#
using ChzzkApi;
using ChzzkApi.Extensions;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
services.AddChzzkApiClient();
var provider = services.BuildServiceProvider();

var client = new ChzzkClient(
    provider.GetRequiredService<IHttpClientFactory>(),
    Environment.GetEnvironmentVariable("CHZZK_CLIENT_ID")!,
    Environment.GetEnvironmentVariable("CHZZK_CLIENT_SECRET")!);
```

```C#
const string redirectUrl = "http://localhost:8080/api/path/"; // 해당 Url은 등록한 애플리케이션의 로그인 리디렉션 URL과 동일해야 합니다

var authUrl = client.BuildAuthUrl(redirectUrl, out string state);
var codeTask = ChzzkClient.WaitForAuthCodeAsync(redirectUrl, state);

OpenUrl(authUrl); // 최종 사용자가 authUrl에 접속해서 정보 제공 동의를 해야 합니다

ChzzkUser user = await client.CreateUserAsync(await codeTask, state);
```

### 세션 사용법

세션은 채팅 및 후원과 구독 이벤트를 읽을 수 있는 소켓 연결입니다.\
특정 이벤트를 읽으려면 해당 이벤트를 구독해야 합니다.
- 클라이언트 세션은 여러 유저의 이벤트를 구독할 수 있습니다.
- 유저 세션은 해당 유저의 이벤트만 구독 가능합니다. 

자세한 정보는 [공식 문서의 세션 항목](https://chzzk.gitbook.io/chzzk/chzzk-api/session)을 확인하시길 바랍니다.

```C#
using ChzzkApi.Models;

var session = await user.CreateSessionAsync();
//var session = await client.CreateSessionAsync();

session.ChatReceived += chat =>
{
    Console.WriteLine($"{chat.Profile.Nickname}: {chat.Content}");
    return Task.CompletedTask;
};

await session.ConnectAsync();
await session.SubscribeEventAsync(user, EventType.Chat); // 이벤트 구독은 세션 연결 후에만 가능합니다 

Console.ReadLine();

await session.DisconnectAsync();
```

### Rule
> [!WARNING]
> 아직 검증이 완벽하지 못한 기능입니다. 예상과 다른 결과가 발생할 수 있습니다.

Rule은 기본적으로 모든 응답이 거치는 후처리입니다. Rule의 `ctx.Client`나 `ctx.User`에서 실행하는 API 호출은 Rule을 무시합니다.
- `ctx.Response`를 사용하여 처리할 응답을 읽을 수 있습니다.
- `ctx.ResendAsync()`로 동일한 요청을 재시도하며, `ctx.Response`를 갱신합니다.

```C#
using System.Net;
using ChzzkApi.Exceptions;

// 유효하지 않은 토큰 재발급 후 재시도하는 규칙
client.Rules.Add("TokenRefresh", async (ctx, ct) =>
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

                if (refreshed.Code != HttpStatusCode.OK)
                    throw new ChzzkApiException(refreshed);
            }

            await ctx.ResendAsync(ct);
            break;
        default:
            break;
    }
});
```

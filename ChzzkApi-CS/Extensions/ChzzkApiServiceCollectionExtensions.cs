using Microsoft.Extensions.DependencyInjection;

namespace ChzzkApi_CS.Extensions;

public static class ChzzkApiServiceCollectionExtensions
{
    public static IServiceCollection AddChzzkApiClient(this IServiceCollection services)
    {
        services.AddHttpClient(nameof(ChzzkApi), client =>
        {
            client.BaseAddress = new Uri("https://openapi.chzzk.naver.com");
        });

        services.AddSingleton<ChzzkApi.Factory>();

        return services;
    }
}

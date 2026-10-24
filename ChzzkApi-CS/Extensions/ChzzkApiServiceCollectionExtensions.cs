using Microsoft.Extensions.DependencyInjection;

namespace ChzzkApi.Extensions;

public static class ChzzkApiServiceCollectionExtensions
{
    public static IServiceCollection AddChzzkApiClient(this IServiceCollection services)
    {
        services.AddHttpClient(nameof(ChzzkClientApi), client =>
        {
            client.BaseAddress = new Uri("https://openapi.chzzk.naver.com");
        });

        return services;
    }
}

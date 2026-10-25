using Microsoft.Extensions.DependencyInjection;

namespace ChzzkApi.Extensions;

public static class ChzzkApiServiceCollectionExtensions
{
    public static IServiceCollection AddChzzkApiClient(this IServiceCollection services)
    {
        services.AddHttpClient(nameof(ChzzkApi));

        return services;
    }
}

using Microsoft.Extensions.DependencyInjection;

namespace SankhyaAPI.Client.Extensions;

public static class SankhyaApiExtensions
{
    public static IServiceCollection AddSankhyaClient(this IServiceCollection services)
    {
        var typeInterface = typeof(ISankhyaApi);
        var implementation = typeInterface.Assembly
            .GetTypes()
            .Where(t => !t.IsInterface
                        && t != typeInterface
                        && !t.IsGenericType
                        && t.IsAssignableTo(typeInterface));

        foreach (var type in implementation) services.AddScoped(type);
        return services;
    }
}
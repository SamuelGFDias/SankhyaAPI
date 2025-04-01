using Microsoft.Extensions.DependencyInjection;

namespace SankhyaAPI.Client.Extensions;

public static class SankhyaApiExtensions
{
    public static IServiceCollection AddSankhyaClient(this IServiceCollection services)
    {
        Type typeInterface = typeof(ISankhyaApi);
        IEnumerable<Type> implementation = typeInterface.Assembly
                                                        .GetTypes()
                                                        .Where(t => !t.IsInterface
                                                                 && t != typeInterface
                                                                 && !t.IsGenericType
                                                                 && t.IsAssignableTo(typeInterface));

        foreach (Type? type in implementation) services.AddScoped(type);
        return services;
    }
}
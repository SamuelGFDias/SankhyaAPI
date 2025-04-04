using Refit;

namespace SankhyaAPI.Client.ClientFactory;

public static class SankhyaApiClientFactory
{
    internal static Interfaces.ISankhyaServiceClient ConfigureClient(string baseUrl)
    {
        var client = RestService.For<Interfaces.ISankhyaServiceClient>(
            new HttpClient
            {
                BaseAddress = new Uri(baseUrl),
                Timeout = TimeSpan.FromDays(1),
                DefaultRequestHeaders = { { "User-Agent", "Refit" }, { "Accept", "*/*" } }
            },
            new RefitSettings
            {
                ContentSerializer = new XmlContentSerializer()
            });


        return client;
    }

    internal static Interfaces.ISankhyaServiceClient ConfigureClientJson(string baseUrl)
    {
        var client = RestService.For<Interfaces.ISankhyaServiceClient>(
            new HttpClient
            {
                BaseAddress = new Uri(baseUrl),
                Timeout = TimeSpan.FromDays(1),
                DefaultRequestHeaders = { { "User-Agent", "Refit" },{ "Accept", "*/*" } }
            },
            new RefitSettings
            {
                ContentSerializer =
                    new SystemTextJsonContentSerializer()
            });

        return client;
    }

    internal static Interfaces.ISankhyaServiceClient ConfigureClientFile(string baseUrl)
    {
        var client = RestService.For<Interfaces.ISankhyaServiceClient>(
            new HttpClient
            {
                BaseAddress = new Uri(baseUrl),
                Timeout = TimeSpan.FromDays(1),
                DefaultRequestHeaders = { { "User-Agent", "Refit" }, { "Accept", "*/*" } }
            },
            new RefitSettings
            {
                ContentSerializer = new XmlContentSerializer()
            });


        return client;
    }
}
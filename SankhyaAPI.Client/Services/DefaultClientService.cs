using Microsoft.Extensions.Options;
using SankhyaAPI.Client.ClientFactory;
using SankhyaAPI.Client.Providers;

namespace SankhyaAPI.Client.Services;

public abstract class DefaultClientService(IOptions<SankhyaClientSettings> sankhyaApiConfig)
{
    protected readonly Interfaces.ISankhyaServiceClient ClientJson =
        SankhyaApiClientFactory.ConfigureClientJson(sankhyaApiConfig.Value.BaseUrl);

    protected readonly Interfaces.ISankhyaServiceClient ClientXml =
        SankhyaApiClientFactory.ConfigureClient(sankhyaApiConfig.Value.BaseUrl);

    protected readonly Interfaces.ISankhyaServiceClient ClientFile =
        SankhyaApiClientFactory.ConfigureClientFile(sankhyaApiConfig.Value.BaseUrl);
}
using SankhyaAPI.Client.ClientFactory;

namespace SankhyaAPI.Client.Services;

public abstract class DefaultClientService(string baseUrl)
{
    protected readonly Interfaces.ISankhyaServiceClient ClientJson =
        SankhyaApiClientFactory.ConfigureClientJson(baseUrl);

    protected readonly Interfaces.ISankhyaServiceClient ClientXml =
        SankhyaApiClientFactory.ConfigureClient(baseUrl);

    protected readonly Interfaces.ISankhyaServiceClient ClientFile =
        SankhyaApiClientFactory.ConfigureClientFile(baseUrl);
}
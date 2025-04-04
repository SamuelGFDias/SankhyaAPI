using Microsoft.Extensions.Options;
using Refit;
using SankhyaAPI.Client.Envelopes;
using SankhyaAPI.Client.Extensions;
using SankhyaAPI.Client.MetaData;
using SankhyaAPI.Client.Providers;
using SankhyaAPI.Client.Requests;
using SankhyaAPI.Client.Responses;

namespace SankhyaAPI.Client.Services;

public sealed class RelatorioService(IOptions<SankhyaClientSettings> sankhyaApiConfig)
    : SessionService(sankhyaApiConfig)
{
    private readonly SankhyaClientSettings _sankhyaConfig = sankhyaApiConfig.Value;

    public async Task<string> GerarRelatorio(int relatorio, List<Parametro> parametros)
    {
        var envelope = new ServiceRequest<string>()
        {
            RequestBody = new RequestBody<string>
            {
                Relatorio = new Relatorio
                {
                    NuRfe = relatorio.ToString(),
                    Parametros = parametros
                }
            }
        };
        envelope.SetServiceName(EServiceNames.VisualizadorRelatorios);

        try
        {
            await LoginSankhya(_sankhyaConfig.Usuario, _sankhyaConfig.Senha);
            ApiResponse<ServiceResponse<string>> response = await ClientXml.VisualizadorRelatorio(JSessionId, envelope);
            response.Content!.VerificarErros();
            return response.Content!.ResponseBody.Chave!.Valor!;
        }
        catch (Exception ex)
        {
            await LogoutSankhya();
            throw new Exception(ex.Message);
        }
    }

    public async Task<Stream> ObterRelatorio(string key)
    {
        try
        {
            ApiResponse<Stream> response = await ClientFile.ObterRelatorio(JSessionId, key);
            if (response.Content == null)
            {
                throw new Exception("O conteúdo do relatório é nulo");
            }

            Stream? content = response.Content;

            return content;
        }
        catch (Exception ex)
        {
            throw new Exception(ex.Message);
        }
        finally
        {
            await LogoutSankhya();
        }
    }
}

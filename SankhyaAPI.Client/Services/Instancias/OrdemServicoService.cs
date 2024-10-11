using Microsoft.Extensions.Options;
using SankhyaAPI.Client.Entities.Request;
using SankhyaAPI.Client.Entities.Response.LoadEntities;
using SankhyaAPI.Client.Entities.Response.Proxies;
using SankhyaAPI.Client.Interfaces;
using SankhyaAPI.Client.MetaData;
using SankhyaAPI.Client.Providers;

namespace SankhyaAPI.Client.Services.Instancias;

public sealed class OrdemServicoService(IOptions<SankhyaClientSettings> sankhyaConfig) : SessionService(sankhyaConfig), IAutoEnumerableEntityService<OrdemServicoEntity, OrdemServicoRequest>
{
    public async Task<List<OrdemServicoEntity>> Inserir(List<OrdemServicoRequest> requests)
    {
        return await InsertRequest<OrdemServicoEntity, OrdemServicoProxy, OrdemServicoRequest>(requests,
            EntityNames.OrdemServico);
    }

    public async Task<OrdemServicoEntity> Inserir(OrdemServicoRequest request)
    {
        var response = await Inserir([request]);
        return response.First();
    }

    public async Task<List<OrdemServicoEntity>> Atualizar(List<OrdemServicoRequest> requests)
    {
        return await UpdateRequest<OrdemServicoEntity, OrdemServicoProxy, OrdemServicoRequest>(requests,
            EntityNames.OrdemServico);
    }

    public async Task<OrdemServicoEntity> Atualizar(OrdemServicoRequest request)
    {
        var response = await Atualizar([request]);
        return response.First();
    }

    public Task<List<OrdemServicoEntity>> Recuperar(string query)
    {
        return LoadRequest<OrdemServicoEntity, OrdemServicoProxy>(query, EntityNames.OrdemServico);
    }
}
using Microsoft.Extensions.Options;
using SankhyaAPI.Client.Entities.Insert;
using SankhyaAPI.Client.Entities.Request;
using SankhyaAPI.Client.Entities.Response.LoadEntities;
using SankhyaAPI.Client.Entities.Response.Proxies;
using SankhyaAPI.Client.Interfaces;
using SankhyaAPI.Client.MetaData;
using SankhyaAPI.Client.Providers;

namespace SankhyaAPI.Client.Services.Instancias;

public sealed class ItemOrdemServicoService
    (IOptions<SankhyaClientSettings> sankhyaConfig)
    : 
        SessionService(sankhyaConfig),
        IEntityService<ItemOrdemServicoEntity, ItemOrdemServicoRequest, ItemOrdemServicoInsert>
{
    public async Task<List<ItemOrdemServicoEntity>> Inserir(List<ItemOrdemServicoInsert> requests)
    {

        return await InsertRequest<ItemOrdemServicoEntity, ItemOrdemServicoProxy, ItemOrdemServicoInsert>(requests, EntityNames.ItemOrdemServico);
    }

    public async Task<ItemOrdemServicoEntity> Inserir(ItemOrdemServicoInsert request)
    {
        var response = await Inserir([request]);
        return response.First();
    }

    public async Task<List<ItemOrdemServicoEntity>> Atualizar(List<ItemOrdemServicoRequest> requests)
    {
        return await UpdateRequest<ItemOrdemServicoEntity, ItemOrdemServicoProxy, ItemOrdemServicoRequest>(requests,
            EntityNames.ItemOrdemServico);
    }

    public async Task<ItemOrdemServicoEntity> Atualizar(ItemOrdemServicoRequest request)
    {
        var response = await Atualizar([request]);
        return response.First();
    }

    public async Task<List<ItemOrdemServicoEntity>> Recuperar(string query)
    {
        return await LoadRequest<ItemOrdemServicoEntity, ItemOrdemServicoProxy>(query, EntityNames.ItemOrdemServico);
    }
}
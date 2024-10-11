using Microsoft.Extensions.Options;
using SankhyaAPI.Client.Entities.Insert;
using SankhyaAPI.Client.Entities.Request;
using SankhyaAPI.Client.Entities.Response.LoadEntities;
using SankhyaAPI.Client.Entities.Response.Proxies;
using SankhyaAPI.Client.Interfaces;
using SankhyaAPI.Client.MetaData;
using SankhyaAPI.Client.Providers;

namespace SankhyaAPI.Client.Services.Instancias;

public sealed class ItemNotaService
    (IOptions<SankhyaClientSettings> sankhyaApiConfig)
    : 
        SessionService(sankhyaApiConfig), 
        IEntityService<ItemCabecalhoNotaEntity, ItemCabecalhoNotaRequest, ItemCabecalhoNotaInsert>
{
    public async Task<List<ItemCabecalhoNotaEntity>> Inserir(List<ItemCabecalhoNotaInsert> requests)
    {
        return await InsertRequest<ItemCabecalhoNotaEntity, ItemCabecalhoNotaProxy, ItemCabecalhoNotaInsert>(requests, EntityNames.ItemNota);
    }

    public async Task<ItemCabecalhoNotaEntity> Inserir(ItemCabecalhoNotaInsert request)
    {
        var response = await Inserir([request]);
        return response.First();
    }

    public async Task<List<ItemCabecalhoNotaEntity>> Atualizar(List<ItemCabecalhoNotaRequest> requests)
    {
        return await UpdateRequest<ItemCabecalhoNotaEntity, ItemCabecalhoNotaProxy, ItemCabecalhoNotaRequest>(requests,
            EntityNames.ItemNota);
    }

    public async Task<ItemCabecalhoNotaEntity> Atualizar(ItemCabecalhoNotaRequest request)
    {
        var response = await Atualizar([request]);
        return response.First();
    }

    public async Task<List<ItemCabecalhoNotaEntity>> Recuperar(string query)
    {
        return await LoadRequest<ItemCabecalhoNotaEntity, ItemCabecalhoNotaProxy>(query, EntityNames.ItemNota);
    }
}
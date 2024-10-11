using Microsoft.Extensions.Options;
using SankhyaAPI.Client.Entities.Request;
using SankhyaAPI.Client.Entities.Response.LoadEntities;
using SankhyaAPI.Client.Entities.Response.Proxies;
using SankhyaAPI.Client.Interfaces;
using SankhyaAPI.Client.MetaData;
using SankhyaAPI.Client.Providers;

namespace SankhyaAPI.Client.Services.Instancias;

public sealed class CabecalhoNotaService
    (IOptions<SankhyaClientSettings> sankhyaApiConfig)
    : 
        SessionService(sankhyaApiConfig), 
        IAutoEnumerableEntityService<CabecalhoNotaEntity, CabecalhoNotaRequest>
{
    public async Task<List<CabecalhoNotaEntity>> Inserir(List<CabecalhoNotaRequest> requests)
    {
        return await InsertRequest<CabecalhoNotaEntity, CabecalhoNotaProxy, CabecalhoNotaRequest>(requests,
            EntityNames.CabecalhoNota);
    }

    public async Task<CabecalhoNotaEntity> Inserir(CabecalhoNotaRequest request)
    {
        var response = await Inserir([request]);
        return response.First();
    }

    public async Task<List<CabecalhoNotaEntity>> Atualizar(List<CabecalhoNotaRequest> request)
    {
        return await UpdateRequest<CabecalhoNotaEntity, CabecalhoNotaProxy, CabecalhoNotaRequest>(request,
            EntityNames.CabecalhoNota);
    }

    public async Task<CabecalhoNotaEntity> Atualizar(CabecalhoNotaRequest requests)
    {
        var response = await Atualizar([requests]);
        return response.First();
    }

    public async Task<List<CabecalhoNotaEntity>> Recuperar(string query)
    {
        return await LoadRequest<CabecalhoNotaEntity, CabecalhoNotaProxy>(query, EntityNames.CabecalhoNota);
    }
}
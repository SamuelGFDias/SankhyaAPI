using Microsoft.Extensions.Options;
using SankhyaAPI.Client.Entities.Request;
using SankhyaAPI.Client.Entities.Response.LoadEntities;
using SankhyaAPI.Client.Entities.Response.Proxies;
using SankhyaAPI.Client.Interfaces;
using SankhyaAPI.Client.MetaData;
using SankhyaAPI.Client.Providers;

namespace SankhyaAPI.Client.Services.Instancias;

public sealed class LocalFinanceiroService
    (IOptions<SankhyaClientSettings> sankhyaApiConfig)
    : 
        SessionService(sankhyaApiConfig), 
        IAutoEnumerableEntityService<LocalFinanceiroEntity, LocalFinanceiroRequest>
{
    public async Task<List<LocalFinanceiroEntity>> Inserir(List<LocalFinanceiroRequest> requests)
    {
        return await InsertRequest<LocalFinanceiroEntity, LocalFinanceiroProxy, LocalFinanceiroRequest>(requests, EntityNames.LocalFinanceiro);
    }

    public async Task<LocalFinanceiroEntity> Inserir(LocalFinanceiroRequest request)
    {
       var response = await Inserir([request]);
       return response.First();
    }

    public async Task<List<LocalFinanceiroEntity>> Atualizar(List<LocalFinanceiroRequest> requests)
    {
        return await UpdateRequest<LocalFinanceiroEntity, LocalFinanceiroProxy, LocalFinanceiroRequest>(requests, EntityNames.LocalFinanceiro);
    }

    public async Task<LocalFinanceiroEntity> Atualizar(LocalFinanceiroRequest request)
    {
        var response = await Atualizar([request]);
        return response.First();
    }

    public async Task<List<LocalFinanceiroEntity>> Recuperar(string query)
    {
        return await LoadRequest<LocalFinanceiroEntity, LocalFinanceiroProxy>(query, EntityNames.LocalFinanceiro);
    }
}
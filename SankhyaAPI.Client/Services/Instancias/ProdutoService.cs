using Microsoft.Extensions.Options;
using SankhyaAPI.Client.Entities.Request;
using SankhyaAPI.Client.Entities.Response.LoadEntities;
using SankhyaAPI.Client.Entities.Response.Proxies;
using SankhyaAPI.Client.Interfaces;
using SankhyaAPI.Client.MetaData;
using SankhyaAPI.Client.Providers;

namespace SankhyaAPI.Client.Services.Instancias;

public sealed class ProdutoService
    (IOptions<SankhyaClientSettings> sankhyaConfig)
    : 
        SessionService(sankhyaConfig), 
        IAutoEnumerableEntityService<ProdutoEntity, ProdutoRequest>
{
    public async Task<List<ProdutoEntity>> Inserir(List<ProdutoRequest> requests)
    {
        return await InsertRequest<ProdutoEntity, ProdutoProxy, ProdutoRequest>(requests, EntityNames.Produto);
    }

    public async Task<ProdutoEntity> Inserir(ProdutoRequest request)
    {
        var response = await Inserir([request]);
        return response.First();
    }

    public async Task<List<ProdutoEntity>> Atualizar(List<ProdutoRequest> requests)
    {
        return await UpdateRequest<ProdutoEntity, ProdutoProxy, ProdutoRequest>(requests, EntityNames.Produto);
    }

    public async Task<ProdutoEntity> Atualizar(ProdutoRequest request)
    {
        var response = await Atualizar([request]);
        return response.First();
    }

    public async Task<List<ProdutoEntity>> Recuperar(string query)
    {
        return await LoadRequest<ProdutoEntity, ProdutoProxy>(query, EntityNames.Produto);
    }
}
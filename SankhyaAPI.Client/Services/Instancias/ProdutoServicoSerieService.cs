using Microsoft.Extensions.Options;
using SankhyaAPI.Client.Entities.Request;
using SankhyaAPI.Client.Entities.Response.LoadEntities;
using SankhyaAPI.Client.Entities.Response.Proxies;
using SankhyaAPI.Client.Interfaces;
using SankhyaAPI.Client.MetaData;
using SankhyaAPI.Client.Providers;

namespace SankhyaAPI.Client.Services.Instancias;

public sealed class ProdutoServicoSerieService
    (IOptions<SankhyaClientSettings> sankhyaApiConfig)
    : 
        SessionService(sankhyaApiConfig), 
        IAutoEnumerableEntityService<ProdutoServicoSerieEntity, ProdutoServicoSerieRequest>
{
    public async Task<List<ProdutoServicoSerieEntity>> Inserir(List<ProdutoServicoSerieRequest> requests)
    {
        return await
            InsertRequest<ProdutoServicoSerieEntity, ProdutoServicoSerieProxy, ProdutoServicoSerieRequest>(
                requests, EntityNames.ProdutoServicoSerie);
    }

    public async Task<ProdutoServicoSerieEntity> Inserir(ProdutoServicoSerieRequest request)
    {
        var response = await Inserir([request]);
        return response.First();
    }

    public async Task<List<ProdutoServicoSerieEntity>> Atualizar(List<ProdutoServicoSerieRequest> requests)
    {
        return await
            UpdateRequest<ProdutoServicoSerieEntity, ProdutoServicoSerieProxy, ProdutoServicoSerieRequest>(
                requests, EntityNames.ProdutoServicoSerie);
    }

    public async Task<ProdutoServicoSerieEntity> Atualizar(ProdutoServicoSerieRequest request)
    {
        var response = await Atualizar([request]);
        return response.First();
    }

    public Task<List<ProdutoServicoSerieEntity>> Recuperar(string query)
    {
        return LoadRequest<ProdutoServicoSerieEntity, ProdutoServicoSerieProxy>(query,
            EntityNames.ProdutoServicoSerie);
    }
}
using Microsoft.Extensions.Options;
using SankhyaAPI.Client.Interfaces;
using SankhyaAPI.Client.Providers;
using SankhyaAPI.Client.Requests;

namespace SankhyaAPI.Client.Services;

#region Explicit Key Entities

public abstract class
    BaseService<TResponse, TProxy, TRequest, TInsert>(
        IOptions<SankhyaClientSettings> sankhyaApiConfig,
        string entityName)
    : SessionService(sankhyaApiConfig), IBaseService<TResponse, TRequest, TInsert>
    where TResponse : class, IProxysable<TResponse, TProxy>, new()
    where TRequest : class, IObjectWithKey
    where TInsert : class
    where TProxy : class

{
    public async Task<List<TResponse>> Inserir(List<TInsert> requests)
    {
        return await InsertRequest<TResponse, TProxy, TInsert>(requests, entityName);
    }

    public async Task<TResponse> Inserir(TInsert request)
    {
        var response = await Inserir([request]);
        return response.First();
    }

    public async Task<List<TResponse>> Atualizar(List<TRequest> requests)
    {
        return await UpdateRequest<TResponse, TProxy, TRequest>(requests, entityName);
    }

    public async Task<TResponse> Atualizar(TRequest request)
    {
        var response = await Atualizar([request]);
        return response.First();
    }

    public async Task<List<TResponse>> Recuperar(string query)
    {
        return await LoadRequest<TResponse, TProxy>(query, entityName);
    }
}

#endregion

#region AutoEnumrable Entities

public abstract class
    BaseService<TResponse, TProxy, TRequest>(
        IOptions<SankhyaClientSettings> sankhyaApiConfig,
        string entityName)
    : SessionService(sankhyaApiConfig), IBaseService<TResponse, TRequest>
    where TResponse : class, IProxysable<TResponse, TProxy>, new()
    where TRequest : class, IObjectWithKey
    where TProxy : class

{
    public async Task<List<TResponse>> Inserir(List<TRequest> requests)
    {
        return await InsertRequest<TResponse, TProxy, TRequest>(requests, entityName);
    }

    public async Task<TResponse> Inserir(TRequest request)
    {
        var response = await Inserir([request]);
        return response.First();
    }

    public async Task<List<TResponse>> Atualizar(List<TRequest> requests)
    {
        return await UpdateRequest<TResponse, TProxy, TRequest>(requests, entityName);
    }

    public async Task<TResponse> Atualizar(TRequest request)
    {
        var response = await Atualizar([request]);
        return response.First();
    }

    public async Task<List<TResponse>> Recuperar(string query)
    {
        return await LoadRequest<TResponse, TProxy>(query, entityName);
    }
}

#endregion
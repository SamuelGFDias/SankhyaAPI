using System.Xml.Serialization;
using Microsoft.Extensions.Options;
using SankhyaAPI.Client.Interfaces;
using SankhyaAPI.Client.Providers;

namespace SankhyaAPI.Client.Services;

public abstract class
    BaseService<T>(
        IOptions<SankhyaClientSettings> sankhyaApiConfig,
        string entityName)
    : SessionService(sankhyaApiConfig), IBaseService<T>
    where T : class, IXmlSerializable, new()

{
    public async Task<List<T>> Inserir(List<T> requests)
    {
        return await InsertRequest<T>(requests, entityName);
    }

    public async Task<T> Inserir(T request)
    {
        var response = await Inserir([request]);
        return response.First();
    }

    public async Task<List<T>> Atualizar(List<T> requests)
    {
        return await UpdateRequest<T>(requests, entityName);
    }

    public async Task<T> Atualizar(T request)
    {
        var response = await Atualizar([request]);
        return response.First();
    }

    public async Task<List<T>> Recuperar(string query)
    {
        return await LoadRequest<T>(query, entityName);
    }
}
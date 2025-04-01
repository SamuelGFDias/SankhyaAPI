using System.Xml.Serialization;
using Microsoft.Extensions.Options;
using SankhyaAPI.Client.Interfaces;
using SankhyaAPI.Client.Providers;

namespace SankhyaAPI.Client.Services;

/// <summary>
/// Classe para abstração de serviços de CRUD.
/// </summary>
/// <typeparam name="T"></typeparam>
/// <param name="sankhyaApiConfig"></param>
/// <param name="entityName">Enumerador com atributo <see cref="XmlEnumAttribute"/> implementado para serialização da classe para XML</param>
public abstract class
    BaseService<T>(
        IOptions<SankhyaClientSettings> sankhyaApiConfig,
        Enum entityName)
    : SessionService(sankhyaApiConfig), IBaseService<T>
    where T : class, IModelBase, new()

{
    /// <summary>
    /// Método para inserir linhas no banco de dados.
    /// </summary>
    /// <param name="requests" ref= "T"> Espera uma lista de <typeparamref name="T"/> para inserir.</param>
    /// <returns>Retorna uma lista de <typeparamref name="T"/></returns>
    public async Task<List<T>> CreateManyAsync(List<T> requests)
    {
        return await CreateRequest(requests, entityName);
    }

    /// <summary>
    /// Método para inserir uma única linha no banco de dados.
    /// </summary>
    /// <param name="request" ref= "T"> Espera uma instancia de <typeparamref name="T"/> para inserir.</param>
    /// <returns>Retorna uma instância de <typeparamref name="T"/></returns>
    public async Task<T> CreateAsync(T request)
    {
        List<T> response = await CreateManyAsync([request]);
        return response.First();
    }

    /// <summary>
    /// Método para atualizar linhas no banco de dados.
    /// </summary>
    /// <param name="requests" ref= "T"> Espera uma lista de <typeparamref name="T"/> para atualizar.</param>
    /// <returns>Retorna uma lista de <typeparamref name="T"/></returns>
    public async Task<List<T>> UpdateManyAsync(List<T> requests)
    {
        return await UpdateRequest(requests, entityName);
    }

    /// <summary>
    /// Método para atualizar uma única linha no banco de dados.
    /// </summary>
    /// <param name="request" ref= "T"> Espera uma instancia de <typeparamref name="T"/> para atualizar.</param>
    /// <returns>Retorna uma instância de <typeparamref name="T"/></returns>
    public async Task<T> UpdateAsync(T request)
    {
        List<T> response = await UpdateManyAsync([request]);
        return response.First();
    }

    /// <summary>
    /// Retorna uma lista de objetos do tipo <typeparamref name="T"/> de acordo com a query passada.
    /// </summary>
    /// <param name="query"></param>
    /// <returns>Retorna uma lista de <typeparamref name="T"/></returns>
    public async Task<List<T>> FindAsync(string query)
    {
        return await LoadRequest<T>(query, entityName);
    }

    public async Task<List<T>> QueryRawAsync(string script)
    {
        return await Query<T>(script);
    }
    
    public async Task<List<Dictionary<string, object?>>> QueryAsDictionaryAsync(string script)
    {
        return await Query(script);
    }
}
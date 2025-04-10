using Microsoft.Extensions.Options;
using SankhyaAPI.Client.Extensions;
using SankhyaAPI.Client.Interfaces;
using SankhyaAPI.Client.Providers;

namespace SankhyaAPI.Client.Services;

/// <summary>
/// Representa um serviço abstrato base projetado para realizar operações CRUD em um tipo específico de entidade no cliente da API Sankhya.
/// </summary>
/// <typeparam name="T">Uma classe que implementa <see cref="IModelBase"/> e representa o tipo da entidade.</typeparam>
/// <param name="sankhyaApiConfig">Configurações do cliente da API Sankhya.</param>
/// <param name="entityName">Especifica o nome da entidade associada ao serviço.</param>
public abstract class
    BaseService<T>(
        IOptions<SankhyaClientSettings> sankhyaApiConfig,
        string entityName
    )
    : SessionService(sankhyaApiConfig), IBaseService<T>
    where T : SankhyaModelBase, new()
{
    
    /// <summary>
    /// Insere vários registros no banco de dados.
    /// </summary>
    /// <param name="requests">Uma lista de objetos do tipo <typeparamref name="T"/> a serem inseridos.</param>
    /// <returns>Retorna uma lista de objetos do tipo <typeparamref name="T"/> representando os registros inseridos.</returns>
    public async Task<List<T>> CreateManyAsync(List<T> requests)
    {
        return await CreateRequest(requests, entityName);
    }

    /// <summary>
    /// Método para inserir uma única linha no banco de dados.
    /// </summary>
    /// <param name="request">Espera uma instância de <typeparamref name="T"/> para ser inserida.</param>
    /// <returns>Retorna uma instância de <typeparamref name="T"/> ou null caso não haja resultado.</returns>
    public async Task<T?> CreateAsync(T request)
    {
        List<T> response = await CreateManyAsync([request]);
        return response.FirstOrDefault();
    }

    /// <summary>
    /// Atualiza várias entidades no banco de dados.
    /// </summary>
    /// <param name="requests">Uma lista de objetos do tipo <typeparamref name="T"/> representando as entidades a serem atualizadas.</param>
    /// <returns>Retorna uma lista de objetos do tipo <typeparamref name="T"/> representando as entidades atualizadas.</returns>
    public async Task<List<T>> UpdateManyAsync(List<T> requests)
    {
        return await UpdateRequest(requests, entityName);
    }

    /// <summary>
    /// Método para atualizar uma única entidade no banco de dados.
    /// </summary>
    /// <param name="request">Uma instância de <typeparamref name="T"/> representando a entidade a ser atualizada.</param>
    /// <returns>Retorna uma instância de <typeparamref name="T"/> representando a entidade atualizada, ou nulo se nenhuma entidade for encontrada.</returns>
    public async Task<T?> UpdateAsync(T request)
    {
        List<T> response = await UpdateManyAsync([request]);
        return response.FirstOrDefault();
    }

    /// <summary>
    /// Retrieves a list of objects of type <typeparamref name="T"/> based on the provided query.
    /// </summary>
    /// <param name="query">The query string used to fetch objects of type <typeparamref name="T"/>.</param>
    /// <returns>Returns a list of objects of type <typeparamref name="T"/> that match the query.</returns>
    public async Task<List<T>> FindAsync(string query)
    {
        return await LoadRequest<T>(query, entityName);
    }

    /// <summary>
    /// Executes a raw query in the database and retrieves the results as a list of objects of type <typeparamref name="TQuery"/>.
    /// </summary>
    /// <param name="script">The raw SQL query script to be executed.</param>
    /// <returns>Returns a list of objects of type <typeparamref name="TQuery"/> containing the results of the query.</returns>
    public async Task<List<TQuery>> QueryRawAsync<TQuery>(string script)
        where TQuery : class, new()
    {
        return await Query<TQuery>(script);
    }

    /// <summary>
    /// Executes a raw SQL-like script query and returns the result as a list of dictionaries.
    /// </summary>
    /// <param name="script">The SQL-like query script to be executed.</param>
    /// <returns>Returns a list of dictionaries, where each dictionary represents a row from the result, with keys as column names and values as the corresponding data.</returns>
    public async Task<List<Dictionary<string, object?>>> QueryAsDictionaryAsync(string script)
    {
        return await Query(script);
    }
}
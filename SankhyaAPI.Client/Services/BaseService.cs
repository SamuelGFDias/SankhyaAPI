using SankhyaAPI.Client.Extensions;
using SankhyaAPI.Client.Interfaces;

namespace SankhyaAPI.Client.Services;

/// <summary>
/// Representa um serviço abstrato base que fornece operações genéricas para gerenciamento de entidades utilizando o cliente da API Sankhya.
/// </summary>
/// <typeparam name="T">Define o tipo da entidade que deve implementar <see cref="IModelBase"/>.</typeparam>
public abstract class BaseService<T>(string baseUrl, string userName, string password, string entityName)
    : SessionService(baseUrl, userName, password), IBaseService<T>
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
    /// Insere uma única linha no banco de dados.
    /// </summary>
    /// <param name="request">Uma instância de <typeparamref name="T"/> que será inserida.</param>
    /// <returns>Retorna uma instância de <typeparamref name="T"/> representando o registro inserido, ou null se não houver resultado.</returns>
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
    /// Atualiza uma única entidade no banco de dados.
    /// </summary>
    /// <param name="request">Uma instância de <typeparamref name="T"/> representando a entidade a ser atualizada.</param>
    /// <returns>Retorna uma instância de <typeparamref name="T"/> representando a entidade atualizada, ou nulo se nenhuma entidade for encontrada.</returns>
    public async Task<T?> UpdateAsync(T request)
    {
        List<T> response = await UpdateManyAsync([request]);
        return response.FirstOrDefault();
    }

    /// <summary>
    /// Remove um ou mais registros da entidade especificada no banco de dados.
    /// </summary>
    /// <param name="requests">Uma lista de objetos do tipo <typeparamref name="T"/> representando os registros a serem removidos.</param>
    /// <returns>Retorna uma tarefa que representa a operação de exclusão assíncrona.</returns>
    public async Task DeleteAsync(List<T> requests)
    {
        await DeleteRequest(requests, entityName);
    }

    /// <summary>
    /// Remove um registro do banco de dados.
    /// </summary>
    /// <param name="request">Um objeto do tipo <typeparamref name="T"/> que representa o registro a ser removido.</param>
    /// <returns>Uma tarefa assíncrona que indica a conclusão da operação.</returns>
    public async Task DeleteAsync(T request)
    {
        await DeleteAsync([request]);
    }

    /// <summary>
    /// Recupera uma lista de objetos do tipo <typeparamref name="T"/> com base na consulta fornecida.
    /// </summary>
    /// <param name="query">A string de consulta usada para buscar objetos do tipo <typeparamref name="T"/>.</param>
    /// <returns>Retorna uma lista de objetos do tipo <typeparamref name="T"/> que correspondem à consulta.</returns>
    public async Task<List<T>> FindAsync(string query)
    {
        return await LoadRequest<T>(query, entityName);
    }

    /// <summary>
    /// Executa uma consulta SQL crua no banco de dados e retorna os resultados como uma lista de objetos do tipo <typeparamref name="TQuery"/>.
    /// </summary>
    /// <param name="script">O comando SQL bruto a ser executado.</param>
    /// <returns>Retorna uma lista de objetos do tipo <typeparamref name="TQuery"/> contendo os resultados da consulta.</returns>
    public async Task<List<TQuery>> QueryRawAsync<TQuery>(string script)
        where TQuery : class, new()
    {
        return await Query<TQuery>(script);
    }

    /// <summary>
    /// Executa uma consulta de script em formato SQL-like e retorna o resultado como uma lista de dicionários.
    /// </summary>
    /// <param name="script">O script de consulta em formato SQL-like a ser executado.</param>
    /// <returns>Retorna uma lista de dicionários, onde cada dicionário representa uma linha do resultado, com as chaves como nomes das colunas e os valores como os dados correspondentes.</returns>
    public async Task<List<Dictionary<string, object?>>> QueryAsDictionaryAsync(string script)
    {
        return await Query(script);
    }
}
using SankhyaAPI.Client.Envelopes;
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
    /// Busca registros com filtro parametrizado. Use <c>?</c> na expressão no lugar de cada valor e passe os
    /// valores em <paramref name="parameters"/>, na mesma ordem: eles vão separados da expressão, então um valor
    /// vindo do usuário não vira SQL.
    /// </summary>
    /// <example>
    /// <code>
    /// await service.FindAsync("this.NOMEPARC LIKE ? AND this.CODCID = ?",
    ///     [Parameter.Texto(nome + "%"), Parameter.Inteiro(codCid)]);
    /// </code>
    /// </example>
    /// <param name="expression">Expressão de filtro, com <c>?</c> no lugar de cada valor.</param>
    /// <param name="parameters">Valores dos <c>?</c>, em ordem (veja as fábricas de <see cref="Parameter"/>).</param>
    /// <param name="orderBy">Ordenação, por exemplo <c>"this.CODPARC DESC"</c>. Não deve conter entrada do usuário.</param>
    public async Task<List<T>> FindAsync(string expression, IEnumerable<Parameter> parameters, string? orderBy = null)
    {
        return await LoadRequest<T>(expression, entityName, parameters, orderBy);
    }

    /// <summary>
    /// Busca uma página de registros (até 50 por página, limite do Sankhya). Repita com <c>page + 1</c>
    /// enquanto <see cref="SankhyaPage{T}.HasMoreResult"/> for verdadeiro.
    /// </summary>
    /// <param name="expression">Expressão de filtro, com <c>?</c> no lugar de cada valor.</param>
    /// <param name="parameters">Valores dos <c>?</c>, em ordem; nulo quando não há.</param>
    /// <param name="page">Página, começando em 0.</param>
    /// <param name="orderBy">Ordenação. Use uma ordenação estável para paginar sem repetir registros.</param>
    public async Task<SankhyaPage<T>> FindPageAsync(
        string expression,
        IEnumerable<Parameter>? parameters,
        int page,
        string? orderBy = null)
    {
        return await LoadPageRequest<T>(expression, entityName, parameters, orderBy, page);
    }

    /// <summary>
    /// Executa uma consulta SQL crua no banco de dados e retorna os resultados como uma lista de objetos do tipo <typeparamref name="TQuery"/>.
    /// </summary>
    /// <remarks>
    /// O SQL vai para o servidor como está, com o poder do usuário de integração. Nunca concatene nele valores
    /// vindos do usuário: para filtrar entidades com valores externos, use
    /// <see cref="FindAsync(string, IEnumerable{Parameter}, string?)"/>.
    /// O servidor devolve no máximo 5000 linhas; acima disso lança <see cref="Exceptions.SankhyaException"/>
    /// em vez de entregar o resultado cortado.
    /// </remarks>
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
    /// <remarks>
    /// O SQL vai para o servidor como está. Nunca concatene nele valores vindos do usuário.
    /// Acima de 5000 linhas lança <see cref="Exceptions.SankhyaException"/> em vez de entregar o resultado cortado.
    /// </remarks>
    /// <param name="script">O script de consulta em formato SQL-like a ser executado.</param>
    /// <returns>Retorna uma lista de dicionários, onde cada dicionário representa uma linha do resultado, com as chaves como nomes das colunas e os valores como os dados correspondentes.</returns>
    public async Task<List<Dictionary<string, object?>>> QueryAsDictionaryAsync(string script)
    {
        return await Query(script);
    }
}
using SankhyaAPI.Client.Session;
using SankhyaAPI.Client.Transport;

namespace SankhyaAPI.Client.Repositories;

/// <summary>
/// CRUD de uma entidade do Sankhya (loadRecords, saveRecord, removeRecord).
/// </summary>
public interface ISankhyaRepository<T> where T : class, new()
{
    /// <summary>
    /// Nome da entidade (instância) no Sankhya, por exemplo <c>Produto</c>.
    /// </summary>
    string EntityName { get; }

    /// <summary>
    /// Busca por expressão sem parâmetros. Não concatene valores vindos do usuário: use a sobrecarga com parâmetros.
    /// Traz todos os registros que atendem ao filtro, sem limite.
    /// </summary>
    Task<List<T>> FindAsync(string expression, CancellationToken cancellationToken = default);

    /// <summary>
    /// Busca com filtro parametrizado: <c>?</c> na expressão para cada valor, na mesma ordem de
    /// <paramref name="parameters"/>. Os valores vão separados da expressão e não viram SQL.
    /// </summary>
    /// <param name="orderBy">Ordenação (vai em <c>orderByExpression</c>), por exemplo <c>"this.CODPROD DESC"</c>. Não deve conter entrada do usuário.</param>
    Task<List<T>> FindAsync(
        string expression,
        IEnumerable<Parameter> parameters,
        string? orderBy = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Uma página (até 50 registros, limite do Sankhya). Repita com <c>page + 1</c> enquanto
    /// <see cref="SankhyaPage{T}.HasMoreResult"/> for verdadeiro, com uma ordenação estável.
    /// </summary>
    Task<SankhyaPage<T>> FindPageAsync(
        string expression,
        IEnumerable<Parameter>? parameters,
        int page,
        string? orderBy = null,
        CancellationToken cancellationToken = default);

    Task<T?> CreateAsync(T entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Insere vários registros numa única chamada. Devolve os registros gravados, com as chaves geradas.
    /// </summary>
    Task<List<T>> CreateManyAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default);

    Task<T?> UpdateAsync(T entity, CancellationToken cancellationToken = default);

    Task<List<T>> UpdateManyAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default);

    Task DeleteAsync(T entity, CancellationToken cancellationToken = default);

    Task DeleteManyAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default);
}

internal sealed class SankhyaRepository<T>(ISankhyaTransport transport, ISankhyaSession session, string entityName)
    : ISankhyaRepository<T>
    where T : class, new()
{
    public string EntityName { get; } = entityName;

    public Task<List<T>> FindAsync(string expression, CancellationToken cancellationToken = default) =>
        LoadAsync(expression, null, null, cancellationToken);

    public Task<List<T>> FindAsync(
        string expression,
        IEnumerable<Parameter> parameters,
        string? orderBy = null,
        CancellationToken cancellationToken = default) =>
        LoadAsync(expression, parameters.ToList(), orderBy, cancellationToken);

    public async Task<SankhyaPage<T>> FindPageAsync(
        string expression,
        IEnumerable<Parameter>? parameters,
        int page,
        string? orderBy = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(page);

        var body = EntityXml.LoadRecords<T>(EntityName, expression, parameters?.ToList(), orderBy, page).ToList();
        SankhyaXmlResponse response = await session.ExecuteAsync(
            (id, ct) => transport.SendXmlAsync(SankhyaServices.LoadRecords, body, id, idempotent: true, ct),
            cancellationToken);

        (List<T> items, bool hasMore) = EntityXml.ReadEntities<T>(response.ResponseBody);
        return new SankhyaPage<T>(items, page, hasMore);
    }

    public async Task<T?> CreateAsync(T entity, CancellationToken cancellationToken = default) =>
        (await CreateManyAsync([entity], cancellationToken)).FirstOrDefault();

    public Task<List<T>> CreateManyAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default) =>
        SaveAsync(EntityXml.Insert(EntityName, entities.ToList()), cancellationToken);

    public async Task<T?> UpdateAsync(T entity, CancellationToken cancellationToken = default) =>
        (await UpdateManyAsync([entity], cancellationToken)).FirstOrDefault();

    public Task<List<T>> UpdateManyAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default) =>
        SaveAsync(EntityXml.Update(EntityName, entities.ToList()), cancellationToken);

    public Task DeleteAsync(T entity, CancellationToken cancellationToken = default) =>
        DeleteManyAsync([entity], cancellationToken);

    public async Task DeleteManyAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default)
    {
        var body = EntityXml.Remove(EntityName, entities.ToList()).ToList();
        await session.ExecuteAsync(
            (id, ct) => transport.SendXmlAsync(SankhyaServices.RemoveRecord, body, id, idempotent: false, ct),
            cancellationToken);
    }

    private async Task<List<T>> LoadAsync(string expression, IReadOnlyList<Parameter>? parameters, string? orderBy, CancellationToken cancellationToken)
    {
        var body = EntityXml.LoadRecords<T>(EntityName, expression, parameters, orderBy, null).ToList();
        SankhyaXmlResponse response = await session.ExecuteAsync(
            (id, ct) => transport.SendXmlAsync(SankhyaServices.LoadRecords, body, id, idempotent: true, ct),
            cancellationToken);

        return EntityXml.ReadEntities<T>(response.ResponseBody).Items;
    }

    private async Task<List<T>> SaveAsync(IEnumerable<System.Xml.Linq.XElement> body, CancellationToken cancellationToken)
    {
        var elements = body.ToList();
        SankhyaXmlResponse response = await session.ExecuteAsync(
            (id, ct) => transport.SendXmlAsync(SankhyaServices.SaveRecord, elements, id, idempotent: false, ct),
            cancellationToken);

        return EntityXml.ReadEntities<T>(response.ResponseBody).Items;
    }
}

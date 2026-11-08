using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Xml.Linq;
using SankhyaAPI.Client.Mapping;
using SankhyaAPI.Client.Session;
using SankhyaAPI.Client.Transport;

namespace SankhyaAPI.Client.Repositories;

/// <summary>
/// Campos de um update parcial: <see cref="Set{TProp}"/> envia o valor atual do campo, <see cref="Clear{TProp}"/>
/// envia vazio (apaga no servidor). O que não for citado não vai.
/// </summary>
public sealed class UpdateFields<T>
{
    internal List<(string Member, bool Clear)> Fields { get; } = [];

    public UpdateFields<T> Set<TProp>(Expression<Func<T, TProp>> property)
    {
        Fields.Add((MemberName(property), false));
        return this;
    }

    public UpdateFields<T> Clear<TProp>(Expression<Func<T, TProp>> property)
    {
        Fields.Add((MemberName(property), true));
        return this;
    }

    // Aceita também x => (object)x.Campo (o compilador embrulha o acesso num Convert).
    private static string MemberName<TProp>(Expression<Func<T, TProp>> property) =>
        (property.Body is UnaryExpression { NodeType: ExpressionType.Convert, Operand: var operand } ? operand : property.Body) is MemberExpression { Member: var m }
            ? m.Name
            : throw new ArgumentException("Use um acesso simples a propriedade, como x => x.Campo.", nameof(property));
}

/// <summary>
/// CRUD de uma entidade do Sankhya (loadRecords, saveRecord, removeRecord), pelo mapa do modelo.
/// </summary>
public interface ISankhyaRepository<T> where T : class, new()
{
    /// <summary>Nome da entidade (instância) no Sankhya, por exemplo <c>Produto</c>.</summary>
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

    /// <summary>
    /// Insere o registro. A chave auto-numerada gerada pelo servidor é preenchida no próprio objeto.
    /// </summary>
    Task<T?> CreateAsync(T entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Insere vários registros numa única chamada. Devolve os registros gravados e preenche as chaves geradas nos objetos.
    /// </summary>
    Task<List<T>> CreateManyAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atualiza só os campos informados (<c>u =&gt; u.Set(x =&gt; x.Campo).Clear(x =&gt; x.Outro)</c>), mais a chave.
    /// </summary>
    Task<T?> UpdateAsync(T entity, Action<UpdateFields<T>> fields, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atualiza só o que mudou: se o objeto foi registrado com <see cref="Track"/>, manda as diferenças desde então
    /// (valor que virou <c>null</c> vai apagado); senão, usa o estado dos campos <see cref="NullableState{T}"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">O objeto não é rastreado e o modelo não tem campos <see cref="NullableState{T}"/>.</exception>
    Task<T?> UpdateAsync(T entity, CancellationToken cancellationToken = default);

    /// <summary>Como <see cref="UpdateAsync(T, CancellationToken)"/>, para vários registros numa chamada.</summary>
    Task<List<T>> UpdateManyAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atualiza todos os campos mapeados (valor nulo vai apagado). Sobrescreve o que outros mudaram no servidor.
    /// </summary>
    Task<T?> UpdateAllAsync(T entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Guarda uma cópia dos valores atuais do objeto, para o <see cref="UpdateAsync(T, CancellationToken)"/> mandar só o que mudar depois.
    /// </summary>
    T Track(T entity);

    Task DeleteAsync(T entity, CancellationToken cancellationToken = default);

    Task DeleteManyAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default);

    /// <summary>
    /// Consulta fluente: filtros parametrizados, ordenação pela coluna mapeada, <c>StreamAsync</c> paginado,
    /// <c>ModifiedSince</c> e outros atributos do loadRecords.
    /// </summary>
    SankhyaQuery<T> Query();

    /// <summary>
    /// Busca pela chave, com os valores na ordem dos campos da chave no mapa. Devolve <c>null</c> se não existir.
    /// </summary>
    Task<T?> GetAsync(object[] keyValues, CancellationToken cancellationToken = default);
}

internal sealed class SankhyaRepository<T> : ISankhyaRepository<T> where T : class, new()
{
    private static readonly ConditionalWeakTable<object, Dictionary<string, object?>> Snapshots = new();

    private readonly ISankhyaTransport _transport;
    private readonly ISankhyaSession _session;
    private readonly EntityMap _map;
    private readonly bool _verifyExistence;

    public SankhyaRepository(ISankhyaTransport transport, ISankhyaSession session, string? entityName = null, bool verifyExistenceOnUpdate = true)
    {
        _transport = transport;
        _session = session;
        _verifyExistence = verifyExistenceOnUpdate;
        _map = SankhyaMaps.For(typeof(T));
        EntityName = entityName ?? _map.EntityName
                  ?? throw new InvalidOperationException(
                         $"Informe o nome da entidade de {typeof(T).Name}: Entity(\"...\") no mapa, [SankhyaEntity(\"...\")] no modelo ou Repository<T>(\"...\").");
    }

    public string EntityName { get; }

    internal EntityMap Map => _map;

    public SankhyaQuery<T> Query() => new(this);

    public async Task<T?> GetAsync(object[] keyValues, CancellationToken cancellationToken = default)
    {
        if (_map.Keys.Count == 0) throw new InvalidOperationException($"O mapa de {typeof(T).Name} não tem chave.");
        if (keyValues.Length != _map.Keys.Count)
            throw new ArgumentException($"A chave de {typeof(T).Name} tem {_map.Keys.Count} campo(s); foram passados {keyValues.Length} valores.", nameof(keyValues));

        string expression = string.Join(" AND ", _map.Keys.Select(k => $"this.{k.Writes[0].Column} = ?"));
        (List<T> items, _) = await LoadAsync(expression, keyValues.Select(Parameter.From).ToList(), null, 0, null, cancellationToken);
        return items.FirstOrDefault();
    }

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

        (List<T> items, bool hasMore) = await LoadAsync(expression, parameters?.ToList(), orderBy, page, null, cancellationToken);
        return new SankhyaPage<T>(items, page, hasMore);
    }

    public async Task<T?> CreateAsync(T entity, CancellationToken cancellationToken = default) =>
        (await CreateManyAsync([entity], cancellationToken)).FirstOrDefault();

    public async Task<List<T>> CreateManyAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default)
    {
        List<T> list = entities.ToList();
        List<T> saved = await SaveAsync(EntityXml.Insert(_map, EntityName, list), cancellationToken);
        CopyGeneratedKeys(list, saved);
        return saved;
    }

    public async Task<T?> UpdateAsync(T entity, Action<UpdateFields<T>> fields, CancellationToken cancellationToken = default)
    {
        var spec = new UpdateFields<T>();
        fields(spec);
        var selected = spec.Fields.Select(f => new UpdateField(Member(f.Member), f.Clear)).ToList();

        await EnsureExistAsync([entity], cancellationToken);
        List<T> saved = await SaveAsync(EntityXml.Update(_map, EntityName, [entity], _ => selected), cancellationToken);
        Retrack(entity);
        return saved.FirstOrDefault();
    }

    public async Task<T?> UpdateAsync(T entity, CancellationToken cancellationToken = default) =>
        (await UpdateManyAsync([entity], cancellationToken)).FirstOrDefault();

    public async Task<List<T>> UpdateManyAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default)
    {
        List<T> list = entities.ToList();
        var body = EntityXml.Update(_map, EntityName, list.Cast<object>().ToList(), ChangedFields).ToList();
        await EnsureExistAsync(list, cancellationToken);
        List<T> saved = await SaveAsync(body, cancellationToken);
        foreach (T entity in list) Retrack(entity);
        return saved;
    }

    public async Task<T?> UpdateAllAsync(T entity, CancellationToken cancellationToken = default)
    {
        await EnsureExistAsync([entity], cancellationToken);
        List<T> saved = await SaveAsync(
            EntityXml.Update(_map, EntityName, [entity], e => _map.Members.Where(m => m.Writable).Select(m => new UpdateField(m, m.Value(e) == null))),
            cancellationToken);
        Retrack(entity);
        return saved.FirstOrDefault();
    }

    public T Track(T entity)
    {
        Snapshots.AddOrUpdate(entity, Snapshot(entity));
        return entity;
    }

    public Task DeleteAsync(T entity, CancellationToken cancellationToken = default) =>
        DeleteManyAsync([entity], cancellationToken);

    public async Task DeleteManyAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default)
    {
        var body = EntityXml.Remove(_map, EntityName, entities.Cast<object>().ToList()).ToList();
        await SendAsync(SankhyaServices.RemoveRecord, body, idempotent: false, cancellationToken);
    }

    /// <summary>
    /// O saveRecord cria o registro quando a chave não existe. Antes do update, confere as chaves numa consulta só
    /// (por lotes de 100) e recusa as inexistentes.
    /// </summary>
    private async Task EnsureExistAsync(IReadOnlyList<T> entities, CancellationToken cancellationToken)
    {
        if (!_verifyExistence || entities.Count == 0 || _map.Keys.Count == 0) return;

        string KeyOf(object e) => string.Join("\u001f", _map.Keys.Select(k => k.Writes[0].Format(k.Value(e))));

        foreach (T[] chunk in entities.Chunk(100))
        {
            string condition = "(" + string.Join(" AND ", _map.Keys.Select(k => $"this.{k.Writes[0].Column} = ?")) + ")";
            string expression = string.Join(" OR ", chunk.Select(_ => condition));
            var parameters = chunk.SelectMany(e => _map.Keys.Select(k => Parameter.From(k.Value(e)!))).ToList();

            var body = EntityXml.LoadKeys(_map, EntityName, expression, parameters).ToList();
            SankhyaXmlResponse response = await SendAsync(SankhyaServices.LoadRecords, body, idempotent: true, cancellationToken);
            HashSet<string> found = EntityXml.ReadKeys(_map, response.ResponseBody).Select(KeyOf).ToHashSet();

            int missing = chunk.Count(e => !found.Contains(KeyOf(e)));
            if (missing > 0)
                throw new Exceptions.SankhyaNotFoundException(
                    $"{missing} registro(s) de {EntityName} não existe(m). O update foi cancelado porque o Sankhya criaria registros novos.",
                    SankhyaServices.SaveRecord);
        }
    }

    private IEnumerable<UpdateField> ChangedFields(object entity)
    {
        if (Snapshots.TryGetValue(entity, out Dictionary<string, object?>? snapshot))
        {
            foreach (MemberMap member in _map.Members.Where(m => m.Writable && !m.IsKey))
            {
                object? current = member.Value(entity);
                if (!Equals(snapshot.GetValueOrDefault(member.Name), current))
                    yield return new UpdateField(member, current == null);
            }

            yield break;
        }

        if (!_map.Members.Any(m => m.NullableState != null))
            throw new InvalidOperationException(
                $"Para atualizar {typeof(T).Name}, informe os campos (UpdateAsync(entidade, u => u.Set(...))), "
              + "registre o objeto com Track antes de alterá-lo ou use UpdateAllAsync.");

        foreach (MemberMap member in _map.Members.Where(m => m.Writable && m.NullableState != null))
        {
            EPropertyState state = member.State(entity);
            if (state != EPropertyState.UnSet) yield return new UpdateField(member, state == EPropertyState.Clear);
        }
    }

    private void Retrack(T entity)
    {
        if (Snapshots.TryGetValue(entity, out _)) Track(entity);
    }

    private Dictionary<string, object?> Snapshot(object entity) =>
        _map.Members.Where(m => m.Writable && !m.IsKey).ToDictionary(m => m.Name, m => m.Value(entity));

    private MemberMap Member(string name) =>
        _map.Members.FirstOrDefault(m => m.Name == name)
     ?? throw new ArgumentException($"A propriedade {typeof(T).Name}.{name} não está mapeada.", nameof(name));

    private void CopyGeneratedKeys(List<T> sent, List<T> saved)
    {
        MemberMap? auto = _map.Keys.FirstOrDefault(k => k.AutoNumbered);
        if (auto?.Set == null || saved.Count != sent.Count) return;

        for (int i = 0; i < sent.Count; i++)
        {
            object? generated = auto.Value(saved[i]);
            if (generated != null) auto.Assign(sent[i], generated);
        }
    }

    private async Task<List<T>> LoadAsync(string expression, IReadOnlyList<Parameter>? parameters, string? orderBy, CancellationToken cancellationToken) =>
        (await LoadAsync(expression, parameters, orderBy, null, null, cancellationToken)).Items;

    internal async Task<(List<T> Items, bool HasMore)> LoadAsync(
        string expression,
        IReadOnlyList<Parameter>? parameters,
        string? orderBy,
        int? page,
        IEnumerable<XAttribute>? attributes,
        CancellationToken cancellationToken)
    {
        var body = EntityXml.LoadRecords(_map, EntityName, expression, parameters, orderBy, page, attributes).ToList();
        SankhyaXmlResponse response = await SendAsync(SankhyaServices.LoadRecords, body, idempotent: true, cancellationToken);
        return EntityXml.ReadEntities<T>(_map, response.ResponseBody);
    }

    private async Task<List<T>> SaveAsync(IEnumerable<XElement> body, CancellationToken cancellationToken)
    {
        SankhyaXmlResponse response = await SendAsync(SankhyaServices.SaveRecord, body.ToList(), idempotent: false, cancellationToken);
        return EntityXml.ReadEntities<T>(_map, response.ResponseBody).Items;
    }

    private Task<SankhyaXmlResponse> SendAsync(string service, List<XElement> body, bool idempotent, CancellationToken cancellationToken) =>
        _session.ExecuteAsync((id, ct) => _transport.SendXmlAsync(service, body, id, idempotent, ct), cancellationToken);
}

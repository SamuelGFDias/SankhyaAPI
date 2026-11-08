namespace SankhyaAPI.Client.Mapping;

/// <summary>
/// Coluna escrita por um membro: nome no Sankhya e como formatar o valor do membro para ela.
/// </summary>
internal sealed record WriteColumn(string Column, Func<object?, string?> Format);

/// <summary>
/// Como um membro é lido: as colunas de origem e a composição do valor a partir do texto de cada uma.
/// </summary>
internal sealed record ReadSpec(IReadOnlyList<string> Columns, Func<string?[], object?> Compose);

/// <summary>
/// Membro do modelo de domínio mapeado (propriedade).
/// </summary>
internal sealed class MemberMap
{
    public required string Name { get; init; }
    public required Type Type { get; init; }
    public required Func<object, object?> Get { get; init; }
    public Action<object, object?>? Set { get; init; }

    public List<WriteColumn> Writes { get; } = [];
    public ReadSpec? Read { get; set; }

    /// <summary>Caminho da entidade ligada (<c>&lt;entity path="..."&gt;</c>), para membro lido de outra entidade.</summary>
    public string? JoinPath { get; set; }

    /// <summary>Campo pedido na entidade ligada (o nome na resposta é <c>Caminho_Campo</c>).</summary>
    public string? JoinField { get; set; }

    /// <summary>Lido dos campos de apresentação que o servidor acrescenta (não é pedido).</summary>
    public bool Presentation { get; set; }

    public bool IsKey { get; set; }
    public bool AutoNumbered { get; set; }

    /// <summary>Valor padrão do tipo (<c>default(T)</c> encaixotado), para saber se a chave auto-numerada foi preenchida.</summary>
    public object? DefaultValue { get; init; }

    /// <summary>Acesso ao estado quando o membro é <see cref="NullableState{T}"/>.</summary>
    public INullableStateAccessor? NullableState { get; init; }

    public bool Writable => Writes.Count > 0;

    /// <summary>
    /// Valor "puro" do membro (sem o <see cref="NullableState{T}"/>).
    /// </summary>
    public object? Value(object entity)
    {
        object? raw = Get(entity);
        return NullableState != null && raw != null ? NullableState.Value(raw) : raw;
    }

    /// <summary>
    /// Estado do membro: o do <see cref="NullableState{T}"/>, ou Set/UnSet pela presença de valor (tipo de valor
    /// não anulável conta sempre como informado).
    /// </summary>
    public EPropertyState State(object entity)
    {
        object? raw = Get(entity);
        if (NullableState != null) return raw == null ? EPropertyState.UnSet : NullableState.State(raw);
        return raw == null ? EPropertyState.UnSet : EPropertyState.Set;
    }

    /// <summary>
    /// "Tem valor para a chave": no tipo de valor não anulável, o valor padrão conta como não informado só para
    /// chave auto-numerada (o servidor gera).
    /// </summary>
    public bool HasKeyValue(object entity)
    {
        if (State(entity) != EPropertyState.Set) return false;
        if (!AutoNumbered) return true;
        object? value = Value(entity);
        return value != null && !Equals(value, DefaultValue);
    }

    public void Assign(object entity, object? value)
    {
        if (Set == null) return;
        Set(entity, NullableState != null ? NullableState.Create(value, value == null ? EPropertyState.UnSet : EPropertyState.Set) : value);
    }
}

/// <summary>
/// Mapa compilado de um modelo: entidade, membros, chaves e colunas. Montado uma vez (por mapa fluente,
/// atributos ou código gerado) e reaproveitado.
/// </summary>
internal sealed class EntityMap
{
    public EntityMap(Type type, string? entityName, Func<object> create, IReadOnlyList<MemberMap> members)
    {
        Type = type;
        EntityName = entityName;
        Create = create;
        Members = members;
        Keys = members.Where(m => m.IsKey).ToList();
        Validate();

        RootFields = members.Where(m => m.Read != null && m.JoinPath == null && !m.Presentation)
                            .SelectMany(m => m.Read!.Columns)
                            .Concat(members.SelectMany(m => m.Writes).Select(w => w.Column))
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .ToList();
        Joins = members.Where(m => m.JoinPath != null)
                       .GroupBy(m => m.JoinPath!, StringComparer.OrdinalIgnoreCase)
                       .Select(g => (g.Key, (IReadOnlyList<string>)g.Select(m => m.JoinField!).Distinct().ToList()))
                       .ToList();
        UsesPresentationFields = members.Any(m => m.Presentation);
    }

    public Type Type { get; }
    public string? EntityName { get; }
    public Func<object> Create { get; }
    public IReadOnlyList<MemberMap> Members { get; }
    public IReadOnlyList<MemberMap> Keys { get; }

    /// <summary>Campos pedidos na entidade raiz (<c>&lt;entity path=""&gt;</c>), na ordem em que vêm na resposta.</summary>
    public IReadOnlyList<string> RootFields { get; }

    /// <summary>Entidades ligadas pedidas, com seus campos.</summary>
    public IReadOnlyList<(string Path, IReadOnlyList<string> Fields)> Joins { get; }

    public bool UsesPresentationFields { get; }

    /// <summary>
    /// Ordem das colunas na resposta sem <c>&lt;metadata&gt;</c>: raiz, depois as ligadas como <c>Caminho_Campo</c>.
    /// </summary>
    public IEnumerable<string> ResponseColumns =>
        RootFields.Concat(Joins.SelectMany(j => j.Fields.Select(f => $"{j.Path}_{f}")));

    private void Validate()
    {
        List<MemberMap> auto = Keys.Where(k => k.AutoNumbered).ToList();
        if (auto.Count > 1)
            throw new InvalidOperationException(
                $"O mapa de {Type.Name} tem mais de um campo auto-numerado na chave ({string.Join(", ", auto.Select(a => a.Name))}). Só um pode ser gerado pelo servidor.");

        string? repeated = Members.SelectMany(m => m.Writes).GroupBy(w => w.Column, StringComparer.OrdinalIgnoreCase)
                                  .FirstOrDefault(g => g.Count() > 1)?.Key;
        if (repeated != null)
            throw new InvalidOperationException($"O mapa de {Type.Name} escreve a coluna '{repeated}' mais de uma vez.");

        MemberMap? keyWithoutWrite = Keys.FirstOrDefault(k => k.Writes.Count != 1);
        if (keyWithoutWrite != null)
            throw new InvalidOperationException($"A chave '{keyWithoutWrite.Name}' de {Type.Name} precisa corresponder a exatamente uma coluna.");
    }
}

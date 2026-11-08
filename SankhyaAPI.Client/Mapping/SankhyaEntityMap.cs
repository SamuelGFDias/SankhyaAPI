using System.Linq.Expressions;
using System.Reflection;

namespace SankhyaAPI.Client.Mapping;

/// <summary>
/// Mapeamento de um modelo de domínio para uma entidade do Sankhya, numa classe à parte: o modelo fica sem
/// atributos e sem tipos do pacote.
/// </summary>
/// <example>
/// <code>
/// public sealed class ItemNotaMap : SankhyaEntityMap&lt;ItemNota&gt;
/// {
///     public ItemNotaMap()
///     {
///         Entity("ItemNota");
///         Key(x => x.NumeroNota, "NUNOTA");
///         Key(x => x.Sequencia, "SEQUENCIA").AutoNumbered();
///         Property(x => x.Quantidade, "QTDNEG");
///         Computed(x => x.Entrega)
///             .Writes("DTENTREGA", SankhyaType.Date)
///             .Writes("HRENTREGA", SankhyaType.Time)
///             .Reads&lt;DateOnly?, TimeOnly?&gt;("DTENTREGA", "HRENTREGA", (d, h) => d?.ToDateTime(h ?? TimeOnly.MinValue));
///         Joined(x => x.NomeProduto, "Produto", "DESCRPROD");
///     }
/// }
/// </code>
/// </example>
public abstract class SankhyaEntityMap<T> : ISankhyaEntityMap where T : class, new()
{
    private readonly List<MemberMap> _members = [];
    private string? _entityName;

    Type ISankhyaEntityMap.ModelType => typeof(T);

    EntityMap ISankhyaEntityMap.Build() =>
        new(typeof(T), _entityName, SankhyaAccessorRegistry.TryGet(typeof(T), out Func<object> create, out _) ? create : () => new T(), _members);

    /// <summary>Nome da entidade (instância) no Sankhya, por exemplo <c>Produto</c>.</summary>
    protected void Entity(string name) => _entityName = name;

    /// <summary>
    /// Campo da chave primária. Para chave composta, chame uma vez por campo; no máximo um pode ser
    /// <see cref="KeyBuilder.AutoNumbered"/>.
    /// </summary>
    protected KeyBuilder Key<TProp>(Expression<Func<T, TProp>> property, string column, SankhyaType type = SankhyaType.Auto)
    {
        MemberMap member = Member(property);
        member.IsKey = true;
        MemberColumns.AddColumn(member, column, type, null);
        return new KeyBuilder(member);
    }

    /// <summary>Campo lido e escrito na mesma coluna.</summary>
    protected PropertyBuilder<TProp> Property<TProp>(Expression<Func<T, TProp>> property, string column, SankhyaType type = SankhyaType.Auto)
    {
        MemberMap member = Member(property);
        MemberColumns.AddColumn(member, column, type, null);
        return new PropertyBuilder<TProp>(member, column, type);
    }

    /// <summary>Campo lido e escrito com um conversor próprio.</summary>
    protected void Property<TProp>(Expression<Func<T, TProp>> property, string column, ISankhyaConverter<TProp> converter)
    {
        MemberMap member = Member(property);
        member.Writes.Add(new WriteColumn(column, v => v is TProp value ? converter.Format(value) : null));
        member.Read = new ReadSpec([column], t => string.IsNullOrWhiteSpace(t[0]) ? default(TProp) : converter.Parse(t[0]!.Trim()));
    }

    /// <summary>
    /// Campo calculado: escreve em uma ou mais colunas (<see cref="ComputedBuilder{TProp}.Writes(string, SankhyaType)"/>)
    /// e é recomposto na leitura por função explícita (<c>Reads</c>). Sem <c>Reads</c>, é só de escrita.
    /// </summary>
    protected ComputedBuilder<TProp> Computed<TProp>(Expression<Func<T, TProp>> property) => new(Member(property));

    /// <summary>
    /// Campo só de leitura de uma entidade ligada: pede <c>&lt;entity path="caminho"&gt;&lt;field name="campo"/&gt;</c>
    /// e lê a coluna <c>caminho_campo</c>.
    /// </summary>
    protected void Joined<TProp>(Expression<Func<T, TProp>> property, string path, string field, SankhyaType type = SankhyaType.Auto)
    {
        MemberMap member = Member(property);
        member.JoinPath = path;
        member.JoinField = field;
        member.Read = MemberColumns.SingleRead(member, $"{path}_{field}", type, null);
    }

    /// <summary>
    /// Campo só de leitura dos campos de apresentação que o Sankhya acrescenta à resposta (por exemplo
    /// <c>GrupoUsuario_NOMEGRUPO</c>).
    /// </summary>
    protected void Presentation<TProp>(Expression<Func<T, TProp>> property, string column, SankhyaType type = SankhyaType.Auto)
    {
        MemberMap member = Member(property);
        member.Presentation = true;
        member.Read = MemberColumns.SingleRead(member, column, type, null);
    }

    private MemberMap Member<TProp>(Expression<Func<T, TProp>> expression)
    {
        PropertyInfo property = expression.Body is MemberExpression { Member: PropertyInfo p } ? p
                              : throw new ArgumentException("Use um acesso simples a propriedade, como x => x.Campo.", nameof(expression));

        if (_members.Any(m => m.Name == property.Name))
            throw new InvalidOperationException($"A propriedade {typeof(T).Name}.{property.Name} já foi mapeada.");

        bool isNullableState = typeof(TProp).IsGenericType && typeof(TProp).GetGenericTypeDefinition() == typeof(NullableState<>);
        (Func<object, object?> get, Action<object, object?>? set) = Accessors(expression, property);

        var member = new MemberMap
        {
            Name = property.Name,
            Type = typeof(TProp),
            Get = get,
            Set = set,
            DefaultValue = isNullableState ? null : default(TProp),
            NullableState = isNullableState ? MemberColumns.AccessorFor(default(TProp)!) : null
        };
        _members.Add(member);
        return member;
    }

    /// <summary>
    /// Acessores gerados pelo source generator, quando existem; senão, compilados da expressão (fora de AOT).
    /// </summary>
    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("AOT", "IL3050",
        Justification = "Caminho de reserva: com o source generator do pacote, os acessores gerados são usados e nada é compilado em tempo de execução (o SNK010 avisa quando o generator não pôde gerar).")]
    private static (Func<object, object?> Get, Action<object, object?>? Set) Accessors<TProp>(Expression<Func<T, TProp>> expression, PropertyInfo property)
    {
        if (SankhyaAccessorRegistry.TryGet(typeof(T), out _, out IReadOnlyDictionary<string, SankhyaPropertyAccessor> generated)
         && generated.TryGetValue(property.Name, out SankhyaPropertyAccessor? accessor))
            return (accessor.Get, accessor.Set);

        Func<T, TProp> getter = expression.Compile();
        Action<T, TProp>? setter = property.CanWrite ? CompileSetter<TProp>(property) : null;
        return (o => getter((T)o), setter == null ? null : (o, v) => setter((T)o, (TProp)v!));
    }

    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Idem: reserva quando não há acessor gerado.")]
    private static Action<T, TProp> CompileSetter<TProp>(PropertyInfo property)
    {
        ParameterExpression target = Expression.Parameter(typeof(T));
        ParameterExpression value = Expression.Parameter(typeof(TProp));
        return Expression.Lambda<Action<T, TProp>>(Expression.Assign(Expression.Property(target, property), value), target, value).Compile();
    }

    /// <summary>Configuração adicional de um campo da chave.</summary>
    public sealed class KeyBuilder
    {
        private readonly MemberMap _member;

        internal KeyBuilder(MemberMap member) => _member = member;

        /// <summary>O servidor gera o valor no insert (vai vazio); no máximo um campo por chave.</summary>
        public KeyBuilder AutoNumbered()
        {
            _member.AutoNumbered = true;
            return this;
        }
    }

    /// <summary>Configuração adicional de um campo.</summary>
    public sealed class PropertyBuilder<TProp>
    {
        private readonly MemberMap _member;
        private readonly string _column;
        private readonly SankhyaType _type;

        internal PropertyBuilder(MemberMap member, string column, SankhyaType type)
        {
            _member = member;
            _column = column;
            _type = type;
        }

        /// <summary>Códigos da lista de opções do campo, quando o enum não tem <c>[XmlEnum]</c>.</summary>
        public PropertyBuilder<TProp> Options(params (string Code, TProp Value)[] options)
        {
            var toCode = options.ToDictionary(o => (object)o.Value!, o => o.Code);
            _member.Writes.Clear();
            MemberColumns.AddColumn(_member, _column, _type == SankhyaType.Auto ? SankhyaType.Options : _type, toCode);
            return this;
        }

        /// <summary>Só leitura: o campo nunca vai no insert/update.</summary>
        public PropertyBuilder<TProp> ReadOnly()
        {
            _member.Writes.Clear();
            return this;
        }
    }

    /// <summary>Configuração de um campo calculado.</summary>
    public sealed class ComputedBuilder<TProp>
    {
        private readonly MemberMap _member;

        internal ComputedBuilder(MemberMap member) => _member = member;

        /// <summary>Escreve o valor do campo na coluna, no formato informado (ex.: data e hora em <see cref="SankhyaType.Time"/> vira HHMM).</summary>
        public ComputedBuilder<TProp> Writes(string column, SankhyaType type)
        {
            _member.Writes.Add(new WriteColumn(column, v => SankhyaFormat.Format(v, type)));
            return this;
        }

        /// <summary>Escreve na coluna o resultado da projeção, formatado pelo tipo do resultado.</summary>
        public ComputedBuilder<TProp> Writes<TColumn>(string column, Func<TProp, TColumn> projection, SankhyaType type = SankhyaType.Auto)
        {
            _member.Writes.Add(new WriteColumn(column, v => v is TProp value ? SankhyaFormat.Format(projection(value), type) : null));
            return this;
        }

        /// <summary>Lê o campo de uma coluna, convertendo pelo tipo <typeparamref name="T1"/>.</summary>
        public ComputedBuilder<TProp> Reads<T1>(string column, Func<T1?, TProp> compose)
        {
            string name = _member.Name;
            _member.Read = new ReadSpec([column], t => compose((T1?)SankhyaFormat.Parse(t[0], typeof(T1), SankhyaType.Auto, null, name)));
            return this;
        }

        /// <summary>Recompõe o campo a partir de duas colunas (ex.: data + hora).</summary>
        public ComputedBuilder<TProp> Reads<T1, T2>(string column1, string column2, Func<T1?, T2?, TProp> compose)
        {
            string name = _member.Name;
            _member.Read = new ReadSpec(
                [column1, column2],
                t => compose(
                    (T1?)SankhyaFormat.Parse(t[0], typeof(T1), SankhyaType.Auto, null, name),
                    (T2?)SankhyaFormat.Parse(t[1], typeof(T2), SankhyaType.Auto, null, name)));
            return this;
        }

        /// <summary>Recompõe o campo a partir do texto cru de várias colunas.</summary>
        public ComputedBuilder<TProp> ReadsRaw(string[] columns, Func<string?[], TProp> compose)
        {
            _member.Read = new ReadSpec(columns, t => compose(t));
            return this;
        }
    }
}

/// <summary>
/// Mapa de um modelo (implementado por <see cref="SankhyaEntityMap{T}"/> e pelo código gerado).
/// </summary>
public interface ISankhyaEntityMap
{
    Type ModelType { get; }

    internal EntityMap Build();
}

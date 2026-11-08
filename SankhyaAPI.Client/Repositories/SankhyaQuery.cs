using System.Globalization;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Xml.Linq;
using SankhyaAPI.Client.Mapping;

namespace SankhyaAPI.Client.Repositories;

/// <summary>
/// Consulta de entidades pelo loadRecords, montada de forma fluente.
/// </summary>
/// <example>
/// <code>
/// await foreach (var item in itens.Query()
///                    .Where("this.NUNOTA = ? AND this.QTDNEG > ?", nota, 0m)
///                    .OrderBy(x => x.Sequencia)
///                    .StreamAsync(ct))
/// { ... }
/// </code>
/// </example>
public sealed class SankhyaQuery<T> where T : class, new()
{
    private readonly SankhyaRepository<T> _repository;
    private readonly List<string> _filters = [];
    private readonly List<Parameter> _parameters = [];
    private readonly List<string> _orderBy = [];
    private readonly Dictionary<string, string> _attributes = new();

    internal SankhyaQuery(SankhyaRepository<T> repository) => _repository = repository;

    /// <summary>
    /// Filtro com <c>?</c> no lugar de cada valor; os valores vão como <c>&lt;parameter&gt;</c> (tipo inferido:
    /// texto, inteiro, decimal, data, data e hora, sim/não, enum). Várias chamadas se combinam com <c>AND</c>.
    /// </summary>
    public SankhyaQuery<T> Where(string expression, params object[] values)
    {
        int marks = CountMarks(expression);
        if (marks != values.Length)
            throw new ArgumentException($"A expressão tem {marks} '?' e foram passados {values.Length} valores.", nameof(values));

        _filters.Add(expression);
        _parameters.AddRange(values.Select(Parameter.From));
        return this;
    }

    /// <summary><c>?</c> fora de literais entre aspas simples.</summary>
    private static int CountMarks(string expression)
    {
        int count = 0;
        bool quoted = false;
        foreach (char c in expression)
        {
            if (c == '\'') quoted = !quoted;
            else if (c == '?' && !quoted) count++;
        }

        return count;
    }

    /// <summary>Ordena pela coluna mapeada da propriedade (não aceita texto livre).</summary>
    public SankhyaQuery<T> OrderBy<TProp>(Expression<Func<T, TProp>> property) => AddOrder(property, false);

    /// <summary>Ordena de forma decrescente pela coluna mapeada da propriedade.</summary>
    public SankhyaQuery<T> OrderByDescending<TProp>(Expression<Func<T, TProp>> property) => AddOrder(property, true);

    /// <summary>
    /// Só registros alterados desde a data (<c>modifiedSince</c>). Exige o parâmetro <c>LOGTABOPER</c> habilitado no
    /// Sankhya; sem ele, o servidor responde com erro <c>CORE_E07537</c>.
    /// </summary>
    public SankhyaQuery<T> ModifiedSince(DateTime since)
    {
        _attributes["modifiedSince"] = since.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture);
        return this;
    }

    /// <summary>Ignora campos calculados no servidor (<c>ignoreCalculatedFields</c>).</summary>
    public SankhyaQuery<T> IgnoreCalculatedFields()
    {
        _attributes["ignoreCalculatedFields"] = "true";
        return this;
    }

    /// <summary>
    /// Pede paginação em disco no servidor (<c>useFileBasedPagination</c>), indicada para volume muito grande. A
    /// resposta é a mesma; na homologação o atributo é aceito sem efeito observável.
    /// </summary>
    public SankhyaQuery<T> FileBasedPagination()
    {
        _attributes["useFileBasedPagination"] = "true";
        return this;
    }

    /// <summary>Todos os registros em memória, sem limite. Para volume grande, prefira <see cref="StreamAsync"/>.</summary>
    public async Task<List<T>> ToListAsync(CancellationToken cancellationToken = default) =>
        (await LoadAsync(null, cancellationToken)).Items;

    /// <summary>Uma página (até 50 registros), começando em 0.</summary>
    public async Task<SankhyaPage<T>> ToPageAsync(int page, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(page);
        (List<T> items, bool hasMore) = await LoadAsync(page, cancellationToken);
        return new SankhyaPage<T>(items, page, hasMore);
    }

    /// <summary>
    /// Percorre todos os registros página a página (50 por chamada), sem carregar tudo em memória. Use uma
    /// ordenação estável; o resultado não é um retrato fixo se os dados mudarem durante a leitura.
    /// </summary>
    public async IAsyncEnumerable<T> StreamAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        for (int page = 0;; page++)
        {
            (List<T> items, bool hasMore) = await LoadAsync(page, cancellationToken);
            foreach (T item in items) yield return item;
            if (!hasMore || items.Count == 0) yield break;
        }
    }

    /// <summary>O primeiro registro (pela ordenação informada) ou <c>null</c>.</summary>
    public async Task<T?> FirstOrDefaultAsync(CancellationToken cancellationToken = default) =>
        (await LoadAsync(0, cancellationToken)).Items.FirstOrDefault();

    private SankhyaQuery<T> AddOrder<TProp>(Expression<Func<T, TProp>> property, bool descending)
    {
        string name = (property.Body is UnaryExpression { NodeType: ExpressionType.Convert, Operand: var operand } ? operand : property.Body) is MemberExpression { Member: var m }
                          ? m.Name
                          : throw new ArgumentException("Use um acesso simples a propriedade, como x => x.Campo.", nameof(property));
        MemberMap member = _repository.Map.Members.FirstOrDefault(x => x.Name == name)
                        ?? throw new ArgumentException($"A propriedade {typeof(T).Name}.{name} não está mapeada.", nameof(property));
        string column = member.Writes.FirstOrDefault()?.Column ?? member.Read?.Columns.FirstOrDefault()
                     ?? throw new ArgumentException($"A propriedade {typeof(T).Name}.{name} não tem coluna para ordenar.", nameof(property));

        _orderBy.Add(descending ? $"this.{column} DESC" : $"this.{column}");
        return this;
    }

    private Task<(List<T> Items, bool HasMore)> LoadAsync(int? page, CancellationToken cancellationToken)
    {
        string expression = _filters.Count == 0 ? "1 = 1" : string.Join(" AND ", _filters.Select(f => $"({f})"));
        string? orderBy = _orderBy.Count == 0 ? null : string.Join(", ", _orderBy);
        return _repository.LoadAsync(expression, _parameters, orderBy, page, _attributes.Select(a => new XAttribute(a.Key, a.Value)), cancellationToken);
    }
}

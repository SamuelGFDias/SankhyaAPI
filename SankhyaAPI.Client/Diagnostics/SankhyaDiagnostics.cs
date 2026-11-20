using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SankhyaAPI.Client.Configuration;
using SankhyaAPI.Client.Exceptions;
using SankhyaAPI.Client.Sql;

namespace SankhyaAPI.Client.Diagnostics;

/// <summary>
/// FK sem índice na tabela filha. Ao excluir uma linha de <see cref="ParentTable"/>, o banco confere essa FK e, sem
/// índice que comece por <see cref="Columns"/>, varre <see cref="ChildTable"/> inteira.
/// </summary>
/// <param name="ParentTable">Tabela referenciada (a da entidade excluída).</param>
/// <param name="ChildTable">Tabela que referencia a pai e não tem índice nas colunas da FK.</param>
/// <param name="ForeignKey">Nome da FK.</param>
/// <param name="Columns">Colunas da FK na tabela filha, na ordem da FK.</param>
/// <param name="ChildRows">Linhas da tabela filha (estatística do catálogo).</param>
public sealed record SankhyaIndexSuggestion(string ParentTable, string ChildTable, string ForeignKey, IReadOnlyList<string> Columns, long ChildRows)
{
    /// <summary>Comando sugerido. Revise com o DBA antes de aplicar em produção (tabela grande leva tempo e espaço).</summary>
    public string CreateIndexSql
    {
        get
        {
            string nome = $"IX_{ChildTable}_{string.Join("_", Columns)}";
            if (nome.Length > 128) nome = nome[..128];
            return $"CREATE INDEX {nome} ON {ChildTable} ({string.Join(", ", Columns)})";
        }
    }
}

/// <summary>
/// Diagnósticos de desempenho que leem só o catálogo do banco (SQL Server).
/// </summary>
public interface ISankhyaDiagnostics
{
    /// <summary>
    /// Lista as FKs que referenciam a tabela da entidade e não têm índice nas colunas da tabela filha, da filha maior
    /// para a menor. Cada uma faz a exclusão na tabela pai varrer a filha inteira (e esperar por lock nela).
    /// </summary>
    /// <param name="entityOrTable">Nome da entidade (instância do dicionário, como <c>Contato</c>) ou da tabela (<c>TGFCTT</c>).</param>
    /// <param name="cancellationToken">Cancela a consulta.</param>
    /// <exception cref="NotSupportedException">O dialeto configurado não é SQL Server.</exception>
    Task<IReadOnlyList<SankhyaIndexSuggestion>> FindMissingForeignKeyIndexesAsync(string entityOrTable, CancellationToken cancellationToken = default);
}

internal sealed class SankhyaDiagnostics : ISankhyaDiagnostics
{
    /// <summary>Filhas menores que isso não pesam na exclusão e não viram aviso automático.</summary>
    internal const long MinimumChildRows = 10_000;

    /// <summary>Máximo de avisos por entidade.</summary>
    internal const int MaxWarnings = 10;

    private readonly ISankhyaSql _sql;
    private readonly SankhyaClientOptions _options;
    private readonly ILogger _logger;
    private readonly Func<bool>? _suppressAutomatic;
    private readonly ConcurrentDictionary<string, Lazy<Task>> _checked = new(StringComparer.OrdinalIgnoreCase);

    /// <param name="suppressAutomatic">
    /// Quando devolve true, o aviso automático não roda. Usado com sessão explícita: a consulta iria na mesma sessão
    /// da exclusão que ainda pode estar rodando no servidor e seguraria a próxima operação da aplicação.
    /// </param>
    public SankhyaDiagnostics(ISankhyaSql sql, SankhyaClientOptions options, ILogger? logger = null, Func<bool>? suppressAutomatic = null)
    {
        _sql = sql;
        _options = options;
        _logger = logger ?? NullLogger.Instance;
        _suppressAutomatic = suppressAutomatic;
    }

    public async Task<IReadOnlyList<SankhyaIndexSuggestion>> FindMissingForeignKeyIndexesAsync(string entityOrTable, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entityOrTable);
        if (_options.Dialect != SqlDialect.SqlServer)
            throw new NotSupportedException("O diagnóstico de índices lê o catálogo do SQL Server e não está disponível para Oracle.");

        string tabela = await ResolveTableAsync(entityOrTable.Trim(), cancellationToken);
        List<Dictionary<string, object?>> linhas = await _sql.QueryAsDictionaryAsync(MissingIndexSql(tabela), cancellationToken);

        return linhas.GroupBy(l => Text(l, "FK"))
                     .Select(g =>
                     {
                         var colunas = g.OrderBy(l => Number(l, "ORDEM")).Select(l => Text(l, "COLUNA")).ToList();
                         Dictionary<string, object?> primeira = g.First();
                         return new SankhyaIndexSuggestion(tabela, Text(primeira, "FILHA"), g.Key, colunas, Number(primeira, "LINHAS"));
                     })
                     .OrderByDescending(s => s.ChildRows)
                     .ThenBy(s => s.ChildTable, StringComparer.Ordinal)
                     .ToList();
    }

    /// <summary>
    /// Chamado pelo repositório depois de cada exclusão. Se ela passou de <see cref="SankhyaClientOptions.SlowDeleteThreshold"/>
    /// ou estourou o tempo, roda o diagnóstico em segundo plano, uma vez por entidade, e registra cada índice sugerido como aviso.
    /// </summary>
    internal void ObserveDelete(string entityName, TimeSpan elapsed, Exception? error)
    {
        try
        {
            if (!_options.MissingIndexWarnings || _options.Dialect != SqlDialect.SqlServer) return;
            if (elapsed < _options.SlowDeleteThreshold && !IsTimeout(error)) return;
            if (_suppressAutomatic?.Invoke() == true) return;

            // Lazy: duas exclusões lentas simultâneas da mesma entidade disparam um diagnóstico só.
            _ = _checked.GetOrAdd(entityName, e => new Lazy<Task>(() => Task.Run(() => AdviseAsync(e, elapsed)))).Value;
        } catch
        {
            // Chamado também no catch da exclusão: nunca substitui a exceção original.
        }
    }

    private async Task AdviseAsync(string entityName, TimeSpan elapsed)
    {
        try
        {
            IReadOnlyList<SankhyaIndexSuggestion> sugestoes = await FindMissingForeignKeyIndexesAsync(entityName, CancellationToken.None);
            var relevantes = sugestoes.Where(s => s.ChildRows >= MinimumChildRows).Take(MaxWarnings).ToList();
            if (relevantes.Count == 0)
            {
                Log.NoMissingIndex(_logger, entityName, (long)elapsed.TotalMilliseconds);
                return;
            }

            foreach (SankhyaIndexSuggestion s in relevantes)
                Log.MissingIndex(_logger, entityName, (long)elapsed.TotalMilliseconds, s.ForeignKey, s.ChildTable, string.Join(", ", s.Columns),
                    s.ChildRows, s.ParentTable, s.CreateIndexSql);
        } catch (Exception ex)
        {
            // O diagnóstico é melhor esforço: nunca afeta a operação que o disparou. A entidade sai do cache para a
            // próxima exclusão lenta tentar de novo (a falha costuma vir do mesmo banco sob carga).
            _checked.TryRemove(entityName, out _);
            try { Log.MissingIndexCheckFailed(_logger, entityName, ex); } catch { /* logger que lança não derruba a task */ }
        }
    }

    private static bool IsTimeout(Exception? error)
    {
        for (; error != null; error = error.InnerException)
            if (error is SankhyaTransportException { Kind: SankhyaErrorKind.Timeout } or SankhyaTransportException { HttpStatus: HttpStatusCode.GatewayTimeout })
                return true;
        return false;
    }

    private async Task<string> ResolveTableAsync(string entityOrTable, CancellationToken cancellationToken)
    {
        List<Dictionary<string, object?>> linhas = await _sql.QueryAsDictionaryAsync(
            $"SELECT TOP 1 NOMETAB FROM TDDINS WHERE NOMEINSTANCIA = {entityOrTable}", cancellationToken);
        string? tabela = linhas.Count > 0 ? Convert.ToString(linhas[0].GetValueOrDefault("NOMETAB"), CultureInfo.InvariantCulture)?.Trim() : null;
        return string.IsNullOrEmpty(tabela) ? entityOrTable : tabela;
    }

    /// <summary>
    /// Uma linha por coluna de cada FK que referencia a tabela e não tem índice (clustered ou nonclustered, sem filtro)
    /// cujas primeiras chaves sejam exatamente as colunas da FK, em qualquer ordem. O executeQuery exige começar com
    /// SELECT, então não há CTE.
    /// </summary>
    private static SankhyaSql MissingIndexSql(string tabela) => $"""
        SELECT k.FK, k.FILHA, c.name COLUNA, fkc.constraint_column_id ORDEM, k.LINHAS
        FROM (SELECT fk.object_id, fk.name FK, fk.parent_object_id, OBJECT_NAME(fk.parent_object_id) FILHA,
                     (SELECT COUNT(*) FROM sys.foreign_key_columns x WHERE x.constraint_object_id = fk.object_id) N,
                     (SELECT SUM(p.rows) FROM sys.partitions p WHERE p.object_id = fk.parent_object_id AND p.index_id IN (0, 1)) LINHAS
              FROM sys.foreign_keys fk
              WHERE fk.referenced_object_id = OBJECT_ID({tabela}) AND fk.is_disabled = 0) k
        JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id = k.object_id
        JOIN sys.columns c ON c.object_id = fkc.parent_object_id AND c.column_id = fkc.parent_column_id
        WHERE NOT EXISTS (
            SELECT 1
            FROM sys.indexes i
            JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
            JOIN sys.foreign_key_columns f2 ON f2.constraint_object_id = k.object_id AND f2.parent_column_id = ic.column_id
            WHERE i.object_id = k.parent_object_id AND i.type IN (1, 2) AND i.is_disabled = 0 AND i.has_filter = 0
              AND ic.is_included_column = 0 AND ic.key_ordinal BETWEEN 1 AND k.N
            GROUP BY i.index_id
            HAVING COUNT(*) = k.N)
        ORDER BY k.LINHAS DESC, k.FK, fkc.constraint_column_id
        """;

    private static string Text(Dictionary<string, object?> linha, string coluna) =>
        Convert.ToString(linha.GetValueOrDefault(coluna), CultureInfo.InvariantCulture)?.Trim() ?? "";

    private static long Number(Dictionary<string, object?> linha, string coluna) =>
        linha.GetValueOrDefault(coluna) is { } v ? Convert.ToInt64(v, CultureInfo.InvariantCulture) : 0;
}

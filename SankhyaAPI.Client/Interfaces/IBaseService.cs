using SankhyaAPI.Client.Envelopes;
using SankhyaAPI.Client.Extensions;

namespace SankhyaAPI.Client.Interfaces;

/// <summary>
/// Interface para os serviços base de CRUD
/// </summary>
/// <typeparam name="T"></typeparam>
public interface IBaseService<T> where T : SankhyaModelBase, new()
{
    Task<List<T>> CreateManyAsync(List<T> requests);
    Task<T?> CreateAsync(T request);
    Task<List<T>> UpdateManyAsync(List<T> requests);
    Task<T?> UpdateAsync(T request);
    Task<List<T>> FindAsync(string query);

    // Corpo padrão só para não quebrar quem implementa a interface por conta própria; BaseService<T> implementa os dois.
    Task<List<T>> FindAsync(string expression, IEnumerable<Parameter> parameters, string? orderBy = null) =>
        throw new NotSupportedException("Esta implementação de IBaseService não suporta filtro parametrizado.");

    Task<SankhyaPage<T>> FindPageAsync(string expression, IEnumerable<Parameter>? parameters, int page, string? orderBy = null) =>
        throw new NotSupportedException("Esta implementação de IBaseService não suporta paginação.");
}
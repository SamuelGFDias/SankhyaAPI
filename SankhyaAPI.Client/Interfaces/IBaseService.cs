namespace SankhyaAPI.Client.Interfaces;

/// <summary>
/// Interface para os serviços base de CRUD
/// </summary>
/// <typeparam name="T"></typeparam>
public interface IBaseService<T>
{
    Task<List<T>> CreateManyAsync(List<T> requests);
    Task<T> CreateAsync(T request);
    Task<List<T>> UpdateManyAsync(List<T> requests);
    Task<T> UpdateAsync(T request);
    Task<List<T>> FindAsync(string query);
}
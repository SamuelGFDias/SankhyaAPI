namespace SankhyaAPI.Client.Interfaces;

/// <summary>
/// Interface para os serviços base de CRUD
/// </summary>
/// <typeparam name="T"></typeparam>
public interface IBaseService<T>
{
    Task<List<T>> Inserir(List<T> requests);
    Task<T> Inserir(T request);
    Task<List<T>> Atualizar(List<T> requests);
    Task<T> Atualizar(T request);
    Task<List<T>> Recuperar(string query);
}
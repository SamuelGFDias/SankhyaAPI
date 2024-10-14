namespace SankhyaAPI.Client.Interfaces;

public interface IBaseService<TResponse, TRequest, TInsert>
{
    Task<List<TResponse>> Inserir(List<TInsert> requests);
    Task<TResponse> Inserir(TInsert request);
    Task<List<TResponse>> Atualizar(List<TRequest> requests);
    Task<TResponse> Atualizar(TRequest request);
    Task<List<TResponse>> Recuperar(string query);
}

public interface IBaseService<TResponse, TRequest>
{
    Task<List<TResponse>> Inserir(List<TRequest> requests);
    Task<TResponse> Inserir(TRequest request);
    Task<List<TResponse>> Atualizar(List<TRequest> requests);
    Task<TResponse> Atualizar(TRequest request);
    Task<List<TResponse>> Recuperar(string query);
}
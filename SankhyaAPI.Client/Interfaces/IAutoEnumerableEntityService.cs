namespace SankhyaAPI.Client.Interfaces;

public interface IAutoEnumerableEntityService<TResponse, TRequest>
{
    Task<List<TResponse>> Inserir(List<TRequest> requests);
    Task<TResponse> Inserir(TRequest request);
    Task<List<TResponse>> Atualizar(List<TRequest> requests);
    Task<TResponse> Atualizar(TRequest request);
    Task<List<TResponse>> Recuperar(string query);
}
namespace SankhyaAPI.Client.Interfaces;

public interface IEntityService<TResponse, TRequest, TInsert>
{
    Task<List<TResponse>> Inserir(List<TInsert> requests);
    Task<TResponse> Inserir(TInsert request);
    Task<List<TResponse>> Atualizar(List<TRequest> requests);
    Task<TResponse> Atualizar(TRequest request);
    Task<List<TResponse>> Recuperar(string query);
}
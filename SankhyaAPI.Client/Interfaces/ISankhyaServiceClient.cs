using Refit;
using SankhyaAPI.Client.Requests;
using SankhyaAPI.Client.Responses;

namespace SankhyaAPI.Client.Interfaces;

public interface ISankhyaServiceClient
{
    [Post("/service.sbr?serviceName=MobileLoginSP.login")]
    Task<ApiResponse<ServiceResponse<LoginEntity>>> Login(
        [Body] ServiceRequest<LoginEntity> loginRequestBody);

    [Post("/service.sbr?serviceName=DbExplorerSP.executeQuery")]
    Task<ApiResponse<ServiceResponse<TEntity>>> Query<TEntity>([Header("Cookie")] string cookie,
        [Body] ServiceRequest<TEntity> entityRequestBody)
        where TEntity : class, new();

    [Post("/service.sbr?serviceName=VisualizadorRelatorios.visualizarRelatorio")]
    Task<ApiResponse<ServiceResponse<TEntity>>> VisualizadorRelatorio<TEntity>([Header("Cookie")] string cookie,
        [Body] ServiceRequest<TEntity> entityRequestBody)
        where TEntity : class;

    [Post("/visualizadorArquivos.mge")]
    Task<ApiResponse<Stream>> ObterRelatorio(
        [Header("Cookie")] string cookie,
        [Query] string chaveArquivo,
        [Query] string download = "S",
        [Query] string hidemail = "S");
      

    [Post("/mge/service.sbr?serviceName=MobileLoginSP.logout")]
    Task<ApiResponse<ServiceResponse<object>>> Logout([Header("Cookie")] string cookie);

    [Post("/service.sbr?serviceName=CRUDServiceProvider.loadRecords")]
    Task<ApiResponse<ServiceResponse<TEntity>>> LoadRecordsGeneric<TEntity>([Header("Cookie")] string cookie,
        [Body] ServiceRequest<TEntity> entityRequestBody)
        where TEntity : class, new();

    [Post("/service.sbr?serviceName=CRUDServiceProvider.saveRecord")]
    Task<ApiResponse<ServiceResponse<TEntity>>> SaveRecordsGeneric<TEntity>(
        [Header("Cookie")] string cookie,
        [Body] ServiceRequest<TEntity> entityRequestBody) where TEntity : class, new();

    [Post("/service.sbr?serviceName=CRUDServiceProvider.removeRecord")]
    Task<ApiResponse<ServiceResponse<TEntity>>> RemoveRecordsGeneric<TEntity>(
        [Header("Cookie")] string cookie,
        [Body] ServiceRequest<TEntity> entityRequestBody) where TEntity : class, new();
}
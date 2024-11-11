using SankhyaAPI.Client.Envelopes;
using SankhyaAPI.Client.Extensions;
using SankhyaAPI.Client.MetaData;
using SankhyaAPI.Client.Requests;

namespace SankhyaAPI.Client.Utils;

/// <summary>
/// Utilitário para carregar registros genéricos a partir da API Sankhya.
/// </summary>
public class LoadRecordsGeneric
{
    /// <summary>
    /// Cria um envelope de requisição para carregar registros.
    /// </summary>
    /// <typeparam name="T">Tipo das propriedades da entidade.</typeparam>
    /// <param name="entityName">Nome da entidade a ser carregada.</param>
    /// <param name="expression">Expressão de filtro SQL.</param>
    /// <param name="orderBy">Expressão de ordenação SQL.</param>
    /// <param name="parameters">Lista de parâmetros para a expressão de filtro.</param>
    /// <returns>Um <see cref="Task{ServiceRequest{T}}"/> representando a operação assíncrona.</returns>
    public static ServiceRequest<T> CreateLoadEnvelope<T>(
        Enum entityName,
        string expression = "",
        string? orderBy = null,
        List<Parameter>? parameters = null)
        where T : class, new()
    {
        var envelope = new ServiceRequest<T>
        {
            ServiceName = ServiceNames.CrudServiceProviderLoadRecords,
            RequestBody = new RequestBody<T>
            {
                DataSet = new DataSet
                {
                    DisableRowsLimit = "true",
                    OrderByExpression = orderBy,
                    Criteria = new Criteria
                    {
                        Expression = expression,
                        Parameter = parameters
                    }
                }
            }
        };
        var sankhyaEntity = new SankhyaEntity
        {
            Path = "",
            Field = ObjectUtilsMethods.GetFieldsFromObject(new T())
        };
        envelope.RequestBody.DataSet.SetRootEntity(entityName);
        envelope.RequestBody.DataSet.Entity.Add(sankhyaEntity);
        return envelope;
    }
}
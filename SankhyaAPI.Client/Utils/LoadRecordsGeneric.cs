using SankhyaAPI.Client.Envelopes;
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
    /// <typeparam name="TEnvelope">Tipo do envelope da resposta.</typeparam>
    /// <typeparam name="TPropeties">Tipo das propriedades da entidade.</typeparam>
    /// <param name="entityName">Nome da entidade a ser carregada.</param>
    /// <param name="expression">Expressão de filtro SQL.</param>
    /// <param name="orderBy">Expressão de ordenação SQL.</param>
    /// <param name="parameters">Lista de parâmetros para a expressão de filtro.</param>
    /// <returns>Um <see cref="Task{ServiceRequest{TEnvelope}"/> representando a operação assíncrona.</returns>
    public static Task<ServiceRequest<TEnvelope>> CreateLoadEnvelope<TEnvelope, TPropeties>(
        string entityName, string expression = "",
        string? orderBy = null,
        List<Parameter>? parameters = null)
        where TEnvelope : class
        where TPropeties : class, new()
    {
        var envelope = new ServiceRequest<TEnvelope>
        {
            ServiceName = ServiceNames.CrudServiceProviderLoadRecords,
            RequestBody = new RequestBody<TEnvelope>
            {
                DataSet = new DataSet<TEnvelope>
                {
                    DisableRowsLimit = "true",
                    RootEntity = entityName,
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
            { Path = "", Field = ObjectFromArrayValues.GetFieldsFromObjectJsonAttribOnly(new TPropeties()) };
        envelope.RequestBody.DataSet.Entity.Add(sankhyaEntity);

        return Task.FromResult(envelope);
    }

    public static Task<ServiceRequest<T>> CreateLoadEnvelope<T>(
        string entityName, string expression = "",
        string? orderBy = null,
        List<Parameter>? parameters = null)
        where T : class, new()
    {
        return CreateLoadEnvelope<T, T>(entityName, expression, orderBy, parameters);
    }
}
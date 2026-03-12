using SankhyaAPI.Client.Envelopes;
using SankhyaAPI.Client.Extensions;
using SankhyaAPI.Client.MetaData;
using SankhyaAPI.Client.Requests;

namespace SankhyaAPI.Client.Utils;

public static class SaveRecordsGeneric
{
    #region public Methods

    public static ServiceRequest<T> CreateInsertEnvelope<T>
    (
        List<T> objs,
        string entityName
    )
        where T : class, new()
    {
        SankhyaModelBase.ValidateNullableStateProperties(objs);
        
        ObjectUtilsMethods.ValidarCamposChave(objs);
        var envelope = new ServiceRequest<T>
        {
            RequestBody = new RequestBody
            {
                DataSet = new DataSet
                {
                    DataRow = objs.Select(obj => new DataRow { Entity = obj.AddEntityForFields() }).ToList()
                }
            }
        };
        var sankhyaEntity = new SankhyaEntity { Path = "", Field = ObjectUtilsMethods.GetFieldsFromObject(new T()) };
        envelope.SetServiceName(EServiceNames.SaveRecords);
        envelope.RequestBody.DataSet.SetRootEntity(entityName);
        envelope.RequestBody.DataSet.Entity.Add(sankhyaEntity);
        return envelope;
    }


    public static ServiceRequest<T> CreateUpdateEnvelope<T>
    (
        List<T> objs,
        string entityName
    )
        where T : class, new()
    {
        SankhyaModelBase.ValidateNullableStateProperties(objs);

        ObjectUtilsMethods.ValidarCamposChave(objs, true);
        var envelope = new ServiceRequest<T>
        {
            RequestBody = new RequestBody
            {
                DataSet = new DataSet
                {
                    DataRow = objs
                             .Select
                              (
                                  obj =>
                                  {
                                      var data = new DataRow { Entity = obj.AddEntityForFields(true), };
                                      data.Key.AddKeysAsXml(obj);
                                      return data;
                                  }
                              )
                             .ToList()
                }
            }
        };
        var sankhyaEntity = new SankhyaEntity { Path = "", Field = ObjectUtilsMethods.GetFieldsFromObject(new T()) };
        envelope.SetServiceName(EServiceNames.RemoveRecords);
        envelope.RequestBody.DataSet.SetRootEntity(entityName);
        envelope.RequestBody.DataSet.Entity.Add(sankhyaEntity);
        return envelope;
    }

    #endregion
}
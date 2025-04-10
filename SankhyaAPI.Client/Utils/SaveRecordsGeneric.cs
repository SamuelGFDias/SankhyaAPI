using System.Reflection;
using System.Xml.Linq;
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
            RequestBody = new RequestBody<T>
            {
                DataSet = new DataSet
                {
                    DataRow = objs.Select(obj => new DataRow { Entity = obj.GetEntityForFields() }).ToList()
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
            RequestBody = new RequestBody<T>
            {
                DataSet = new DataSet
                {
                    DataRow = objs
                             .Select
                              (
                                  obj =>
                                  {
                                      var data = new DataRow { Entity = obj.GetEntityForFields(true), };
                                      data.Key.GetKeysAsXml(obj);
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

    #region privateMethods

    private static void GetKeysAsXml<T>(this XElement element, T obj)
    {
        if (obj == null) throw new Exception("A entidade deve ser definida");

        foreach (PropertyInfo? prop in obj.GetType().GetProperties())
        {
            var keyAttribute = prop.GetCustomAttribute<KeyAttribute>();
            if (keyAttribute == null) continue;

            object? value = prop.GetValue(obj);

            if (value == null) throw new Exception($"Chave prim?ria {keyAttribute.ElementName} n?o pode ser nula");

            string? convertedValue = obj.GetFormattedString(prop);

            element.Add(new XElement(keyAttribute.ElementName, convertedValue));
        }
    }

    private static XElement GetEntityForFields<T>(this T obj, bool isUpdate = false)
        where T : class
    {
        var localFields = new XElement("localFields");

        foreach (PropertyInfo? prop in obj.GetType().GetProperties())
        {
            var keyAttribute = prop.GetCustomAttribute<KeyAttribute>();
            if (keyAttribute != null)
            {
                if (isUpdate || keyAttribute.AutoEnumerable) continue;
            }

            string? xmlElementName = prop.GetXmlElementName();
            if (xmlElementName == null) continue;

            object? propertyValue = prop.GetValue(obj);

            bool isNullableState = prop.PropertyType.IsGenericType
                                && prop.PropertyType.GetGenericTypeDefinition() == typeof(NullableState<>);

            EPropertyState? state = null;
            bool isClear = false;
            bool isUnSet = false;

            if (isNullableState && propertyValue != null)
            {
                var stateProperty = propertyValue.GetType().GetProperty(nameof(NullableState<int>.State));
                if (stateProperty != null)
                {
                    object? stateValue = stateProperty.GetValue(propertyValue);
                    if (stateValue is EPropertyState stateEnum)
                    {
                        state = stateEnum;
                        isClear = stateEnum == EPropertyState.Clear;
                        isUnSet = stateEnum == EPropertyState.UnSet;
                    }
                }
            }

            switch (isUpdate)
            {
                case false when state is not EPropertyState.Set:
                case true when isUnSet:
                    continue;
            }

            string? convertedValue = obj.GetFormattedString(prop);

            if (convertedValue != null
             || (isUpdate && isClear))
            {
                localFields.Add
                (
                    isClear
                        ? new XElement(xmlElementName) // UPDATE com Clear
                        : new XElement(xmlElementName, convertedValue)
                );
            }
        }

        return localFields;
    }

    #endregion
}
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Xml.Serialization;
using Azure.Core;
using SankhyaAPI.Client.Envelopes;
using SankhyaAPI.Client.Extensions;

namespace SankhyaAPI.Client.Utils;

public static class ObjectUtilsMethods
{
    #region ObsoleteMethods

    public static void FillFielsFromObjectToTypedObject(object destObj, object srcObj)
    {
        var props1 = PropertiesFromObject(destObj);
        var props2 = PropertiesFromObject(srcObj);
        for (var i = 0; i < props1.Length; i++)
        {
            var value2 = props2[i].GetValue(srcObj);
            if (value2 is not null) props1[i].SetValue(destObj, value2);
        }
    }

    public static void GetListOfObjectsFromObjectMatrix<T>(List<T> objs, List<List<object>> srcObjMatrix)
        where T : class?, new()
    {
        T objectDest;
        foreach (var obj in srcObjMatrix)
        {
            objectDest = new T();
            FillFielsFromObjectList(objectDest, obj);
            objs.Add(objectDest);
        }
    }

    #endregion


    #region PublicMethods

    public static List<Field> GetFieldsFromObject(object obj)
    {
        var props = PropertiesFromObject(obj);

        return props.Select(t => new Field { Nome = t.GetXmlElementName() }).ToList();
    }

    public static string GetFormattedString(object value) =>
        value switch
        {
            float f => f.ToString("G", CultureInfo.InvariantCulture),
            decimal d => d.ToString("G", CultureInfo.InvariantCulture),
            double db => db.ToString("G", CultureInfo.InvariantCulture),
            Enum e => e.GetXmlEnumValue(),
            DateTime dt => dt.ToString("dd/MM/yyyy HH:mm:ss"),
            DateOnly dt => dt.ToString("dd/MM/yyyy"),
            TimeOnly time => time.ToString("HHmm"),
            bool b => b ? "S" : "N",
            _ => value.ToString()!
        };

    public static object? ConvertForPropertyType(string xmlElementValue, PropertyInfo property)
    {
        object? convertedValue;
        try
        {
            var propertyType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;

            if (string.IsNullOrWhiteSpace(xmlElementValue)) return null;

            if (propertyType == typeof(DateTime))
            {
                if (DateTime.TryParse(xmlElementValue, out var date))
                {
                    convertedValue = date;
                }
                else if (DateTime.TryParseExact(xmlElementValue, "ddMMyyyy HH:mm:ss",
                             CultureInfo.InvariantCulture,
                             DateTimeStyles.None,
                             out var dateExact))
                {
                    convertedValue = dateExact;
                }
                else if (DateTime.TryParseExact(xmlElementValue, "ddMMyyyy",
                             CultureInfo.InvariantCulture,
                             DateTimeStyles.None,
                             out var dateExact2))
                {
                    convertedValue = dateExact2;
                }
                else
                {
                    throw new Exception($"Valor '{xmlElementValue}' não é uma data válida");
                }
            }
            else if (propertyType.IsEnum)
            {
                convertedValue = GetEnumValueFromXml(propertyType, xmlElementValue);
            }
            else if (propertyType == typeof(bool))
            {
                convertedValue = xmlElementValue.Equals("S", StringComparison.OrdinalIgnoreCase);
            }
            else
            {
                convertedValue = Convert.ChangeType(xmlElementValue, propertyType);
            }

            return convertedValue;
        }
        catch (Exception ex)
        {
            throw new Exception(
                $"Erro ao converter o valor '{xmlElementValue}' para a propriedade '{property.Name}': {ex.Message}");
        }
    }

    public static List<T> GetListOfObjectsFromDictionary<T>(Dictionary<string, List<dynamic>?> fields)
        where T : class, new()
    {
        if (fields.Values.Count == 0) return [];

        var maxCount = fields.Values.Max(list => list?.Count ?? 0);

        var objs = new List<T>();

        for (int i = 0; i < maxCount; i++)
        {
            var objectDest = new T();
            var props = PropertiesFromObject(objectDest);

            foreach (var prop in props)
            {
                var field = fields.FirstOrDefault(a => a.Key == prop.GetXmlElementName());

                if (field.Value == null || field.Value.Count <= i) continue;

                var value = field.Value[i];

                var convertedValue = ConvertForPropertyType(value?.ToString() ?? "", prop);

                prop.SetValue(objectDest, convertedValue);
            }

            objs.Add(objectDest);
        }

        return objs;
    }

    public static string GetXmlEnumValue(this Enum enumValue)
    {
        var type = enumValue.GetType();
        var value = enumValue.ToString();
        var field = type.GetField(value);

        if (field == null) throw new Exception($"Nenhum enumerador encontrado para a valor passado: {value}");

        if (Attribute.GetCustomAttribute(field, typeof(XmlEnumAttribute)) is not XmlEnumAttribute
            {
                Name: not null
            } attribute)
        {
            throw new Exception("Enumerador não pode conter propriedades sem o atributo XmlEnum implementado");
        }

        return attribute.Name;
    }

    public static string? GetXmlElementName(this PropertyInfo prop)
    {
        var xmlElementAttr = prop.GetCustomAttribute<XmlElementAttribute>();
        var primaryKeyAttr = prop.GetPrimaryKeyAttribute();
        return xmlElementAttr?.ElementName ?? primaryKeyAttr?.ElementName;
    }

    public static void ValidarCamposChave<T>(List<T> objs, bool isUpdate = false)
        where T : class, new()
    {
        var autoEnumerableKeys = GetKeysPropertiesFromObject<T>(true);
        var keys = GetKeysPropertiesFromObject<T>();


        if (!isUpdate
                ? autoEnumerableKeys is not { Count: > 0 }
                : keys is not { Count: > 0 }
           ) return;

        foreach (var item in objs)
        {
            var propertyKeys = item.GetType().GetProperties()
                .Where(!isUpdate
                    ? autoEnumerableKeys.Contains
                    : keys.Contains)
                .ToList();

            foreach (var property in propertyKeys)
            {
                object? value = property.GetValue(item);

                switch (isUpdate)
                {
                    case true when value == null:
                        throw new Exception(
                            "Os campos que fazem parte da chave primária não podem estar vazios em operações de atualização.");
                    case false when value != null:
                        throw new Exception(
                            "Os campos que fazem parte da chave primária com atributos autoenumerados não podem estar preenchidos em operações de inserção.");
                }
            }
        }
    }

    #endregion


    #region PrivateMethods

    private static void FillFielsFromObjectList(object destObj, List<object> srcObjList)
    {
        var props1 = PropertiesFromObject(destObj);
        var minLength = Math.Min(props1.Length, srcObjList.Count);
        for (var i = 0; i < minLength; i++)
        {
            var value2 = srcObjList[i];
            props1[i].SetValue(destObj, value2);
        }
    }

    private static PropertyInfo[] PropertiesFromObject(object obj)
    {
        return obj.GetType().GetProperties();
    }


    private static List<PropertyInfo> GetKeysPropertiesFromObject<T>(bool isAutoEnumerable = false)
        where T : class, new()
    {
        var obj = new T();
        var properties = obj.GetType().GetProperties();

        if (isAutoEnumerable)
            return properties.Where(p =>
                isAutoEnumerable
                    ? p.GetPrimaryKeyAttribute() is { AutoEnumerable: true }
                    : p.GetPrimaryKeyAttribute() != null).ToList();

        return properties.Where(p => p.GetPrimaryKeyAttribute() != null).ToList();
    }

    private static PrimaryKeyElementAttribute? GetPrimaryKeyAttribute(this PropertyInfo prop) =>
        prop.GetCustomAttribute<PrimaryKeyElementAttribute>();


    private static object GetEnumValueFromXml(Type enumType, string xmlValue)
    {
        var fields = enumType.GetFields();
        var matchField = fields.FirstOrDefault(f =>
            f.GetCustomAttribute<XmlEnumAttribute>()?.Name == xmlValue);

        return matchField != null
            ? Enum.Parse(enumType, matchField.Name)
            : throw new ArgumentException($"Valor '{xmlValue}' não é válido para o enum '{enumType.Name}'");
    }

    #endregion
}
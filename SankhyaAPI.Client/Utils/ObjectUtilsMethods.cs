using System.Globalization;
using System.Reflection;
using System.Xml.Serialization;
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

    public static void FillFielsFromObjectList(object destObj, List<object> srcObjList)
    {
        var props1 = PropertiesFromObject(destObj);
        var minLength = Math.Min(props1.Length, srcObjList.Count);
        for (var i = 0; i < minLength; i++)
        {
            var value2 = srcObjList[i];
            props1[i].SetValue(destObj, value2);
        }
    }

    public static PropertyInfo[] PropertiesFromObject(object obj)
    {
        return obj.GetType().GetProperties();
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

            // Preenche as propriedades do objeto atual com os valores do dicionário
            foreach (var field in fields)
            {
                var prop = props.FirstOrDefault(a => a.GetXmlElementName() == field.Key);

                if (prop == null) continue;

                var values = field.Value;
                if (values == null || values.Count <= i) continue;

                var value = values[i];

                var targetType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;

                value = value switch
                {
                    string s => DateTime.TryParseExact(s,
                        "ddMMyyyy HH:mm:ss",
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.None, out var date)
                        ? date
                        : s.Trim(),
                    _ => value
                };

                if (value != null && !targetType.IsInstanceOfType(value))
                    throw new Exception(
                        $"O tipo da propriedade {prop.Name} no objeto {typeof(T)} é {prop.PropertyType} e o valor passado é do tipo {value?.GetType()}");

                var convertedValue = value != null ? Convert.ChangeType(value, targetType) : null;


                prop.SetValue(objectDest, convertedValue);
            }

            // Adiciona o objeto preenchido à lista
            objs.Add(objectDest);
        }

        return objs;
    }

    public static string? GetXmlElementName(this PropertyInfo prop)
    {
        var xmlElementAttr = prop.GetCustomAttribute<XmlElementAttribute>();
        var primaryKeyAttr = prop.GetCustomAttribute<PrimaryKeyElementAttribute>();
        return xmlElementAttr?.ElementName ?? primaryKeyAttr?.ElementName;
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

    private static object GetEnumValueFromXml(Type enumType, string xmlValue)
    {
        var fields = enumType.GetFields();
        var matchField = fields.FirstOrDefault(f =>
            f.GetCustomAttribute<XmlEnumAttribute>()?.Name == xmlValue);

        return matchField != null
            ? Enum.Parse(enumType, matchField.Name)
            : throw new ArgumentException($"Valor '{xmlValue}' não é válido para o enum '{enumType.Name}'");
    }

    public static List<Field> GetFieldsFromObject(object obj)
    {
        var props = PropertiesFromObject(obj);

        return props.Select(t => new Field { Nome = t.GetXmlElementName() }).ToList();
    }

    public static string GetFormattedString(object value) =>
        value switch
        {
            float f => f.ToString("F2", CultureInfo.InvariantCulture),
            decimal d => d.ToString("F2", CultureInfo.InvariantCulture),
            double db => db.ToString("F2", CultureInfo.InvariantCulture),
            Enum e => e.GetXmlEnumValue(),
            DateTime dt => dt.ToString("dd/MM/yyyy HH:mm:ss"),
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
                convertedValue = DateTime.Parse(xmlElementValue);
            }
            else if (propertyType.IsEnum)
            {
                convertedValue = GetEnumValueFromXml(propertyType, xmlElementValue);
            }
            else if (propertyType == typeof(bool))
            {
                if (xmlElementValue.Equals("S", StringComparison.OrdinalIgnoreCase))
                {
                    convertedValue = true;
                }
                else if (xmlElementValue.Equals("N", StringComparison.OrdinalIgnoreCase))
                {
                    convertedValue = false;
                }
                else
                {
                    convertedValue = Convert.ToBoolean(xmlElementValue);
                }
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
}
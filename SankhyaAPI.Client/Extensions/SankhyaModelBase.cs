using System.Reflection;
using SankhyaAPI.Client.Interfaces;
using SankhyaAPI.Client.MetaData;

namespace SankhyaAPI.Client.Extensions;

public class SankhyaModelBase : IModelBase
{
    protected SankhyaModelBase()
    {
        if (!IsNullableProperties(this))
        {
            throw new ArgumentException("Propriedades não nulas estão presentes no objeto");
        }
    }

    private bool IsNullableProperties<T>(T obj) where T : class
    {
        PropertyInfo[] properties = obj.GetType().GetProperties();
        return properties.All(p =>
        {
            Type? isNullable = Nullable.GetUnderlyingType(p.PropertyType);
            bool isValueType = p.PropertyType.IsValueType;
            bool isNullableState = p.PropertyType.IsGenericType && p.PropertyType.GetGenericTypeDefinition() == typeof(NullableState<>);
            return isNullable != null || !isValueType || isNullableState;
        });
    }
}
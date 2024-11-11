using System.Reflection;
using SankhyaAPI.Client.Interfaces;

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
            var isNullable = Nullable.GetUnderlyingType(p.PropertyType);
            bool isValueType = p.PropertyType.IsValueType;
            return isNullable != null || !isValueType;
        });
    }
}
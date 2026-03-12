using System.Text.Json;
using System.Text.Json.Serialization;
using SankhyaAPI.Client.MetaData;

namespace SankhyaAPI.Client.Extensions;

public class NullableStateConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert)
    {
        return typeToConvert.IsGenericType &&
               typeToConvert.GetGenericTypeDefinition() == typeof(NullableState<>);
    }

    public override JsonConverter? CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        var valueType = typeToConvert.GetGenericArguments()[0];
        var converterType = typeof(NullableStateJsonConverter<>).MakeGenericType(valueType);

        return (JsonConverter?)Activator.CreateInstance(converterType);
    }
}

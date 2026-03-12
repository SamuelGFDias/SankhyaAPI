using SankhyaAPI.Client.MetaData;

namespace SankhyaAPI.Client.Extensions;

using System.Text.Json;
using System.Text.Json.Serialization;

public class NullableStateJsonConverter<T> : JsonConverter<NullableState<T>>
{
    public override NullableState<T> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = JsonSerializer.Deserialize<T>(ref reader, options);
        return new NullableState<T>(value);
    }

    public override void Write(Utf8JsonWriter writer, NullableState<T> value, JsonSerializerOptions options)
    {
        JsonSerializer.Serialize(writer, value.Value, options);
    }
}


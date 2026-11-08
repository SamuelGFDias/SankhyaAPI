using System.Text.Json;
using System.Text.Json.Serialization;

namespace SankhyaAPI.Client.Mapping;

/// <summary>
/// Estado de um valor para o update parcial.
/// </summary>
public enum EPropertyState
{
    /// <summary>Não informado: não vai no update (o valor no servidor fica como está).</summary>
    UnSet,

    /// <summary>Informado: vai no update com o valor.</summary>
    Set,

    /// <summary>Apagar: vai no update vazio.</summary>
    Clear
}

/// <summary>
/// Valor com estado, para update parcial com o modelo carregando o que mudou. Imutável: troque o valor
/// atribuindo outro (<c>DescrProd = "X"</c> vira <see cref="EPropertyState.Set"/>), <see cref="Clear"/> para
/// apagar ou <c>default</c>/<see cref="Unset"/> para não mexer.
/// </summary>
[JsonConverter(typeof(NullableStateJsonConverterFactory))]
public readonly struct NullableState<T> : IEquatable<NullableState<T>>, INullableStateValue
{
    EPropertyState INullableStateValue.State => State;

    object? INullableStateValue.BoxedValue => Value;

    object INullableStateValue.Create(object? value, EPropertyState state) =>
        state switch
        {
            EPropertyState.Set => Set((T)value!),
            EPropertyState.Clear => Clear(),
            _ => Unset
        };

    private NullableState(T? value, EPropertyState state)
    {
        Value = value;
        State = state;
    }

    public T? Value { get; }

    public EPropertyState State { get; }

    public bool HasValue => State == EPropertyState.Set && Value != null;

    /// <summary>Valor informado.</summary>
    public static NullableState<T> Set(T value) => new(value, EPropertyState.Set);

    /// <summary>Apagar o campo no update.</summary>
    public static NullableState<T> Clear() => new(default, EPropertyState.Clear);

    /// <summary>Não informado (o mesmo que <c>default</c>).</summary>
    public static NullableState<T> Unset => default;

    /// <summary>
    /// Valor informado; <c>null</c> vira <see cref="Clear"/>. Prefira <see cref="Clear"/> explícito (o analisador
    /// SNK006 avisa sobre a atribuição de <c>null</c>).
    /// </summary>
    public static implicit operator NullableState<T>(T? value) =>
        value is null ? Clear() : new NullableState<T>(value, EPropertyState.Set);

    public static implicit operator T?(NullableState<T> state) => state.Value;

    public bool Equals(NullableState<T> other) => State == other.State && EqualityComparer<T?>.Default.Equals(Value, other.Value);

    public override bool Equals(object? obj) => obj is NullableState<T> other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(State, Value);

    public static bool operator ==(NullableState<T> left, NullableState<T> right) => left.Equals(right);

    public static bool operator !=(NullableState<T> left, NullableState<T> right) => !left.Equals(right);

    public override string ToString() => Value?.ToString() ?? string.Empty;
}

/// <summary>
/// Acesso a um <see cref="NullableState{T}"/> encaixotado sem conhecer o <c>T</c> (e sem reflexão).
/// </summary>
internal interface INullableStateValue
{
    EPropertyState State { get; }
    object? BoxedValue { get; }
    object Create(object? value, EPropertyState state);
}

/// <summary>
/// Leitura e criação de <see cref="NullableState{T}"/> a partir de um protótipo (<c>default</c> encaixotado).
/// </summary>
internal sealed class NullableStateAccessor(INullableStateValue prototype)
{
    public EPropertyState State(object boxed) => ((INullableStateValue)boxed).State;
    public object? Value(object boxed) => ((INullableStateValue)boxed).BoxedValue;
    public object Create(object? value, EPropertyState state) => prototype.Create(value, state);
}

/// <summary>
/// JSON de <see cref="NullableState{T}"/>: o valor puro (<c>null</c> quando não há).
/// </summary>
public sealed class NullableStateJsonConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert) =>
        typeToConvert.IsGenericType && typeToConvert.GetGenericTypeDefinition() == typeof(NullableState<>);

    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("AOT", "IL3050", Justification = "NullableStateJsonConverter<T> não usa código dinâmico além do tipo fechado pelo próprio T.")]
    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2055", Justification = "O tipo genérico fechado vem do próprio NullableState<T> serializado.")]
    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2070", Justification = "Idem.")]
    public override JsonConverter? CreateConverter(Type typeToConvert, JsonSerializerOptions options) =>
        (JsonConverter?)Activator.CreateInstance(typeof(NullableStateJsonConverter<>).MakeGenericType(typeToConvert.GetGenericArguments()[0]));
}

public sealed class NullableStateJsonConverter<T> : JsonConverter<NullableState<T>>
{
    // GetTypeInfo respeita o resolvedor configurado (source generation em AOT).
    public override NullableState<T> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType == JsonTokenType.Null
            ? NullableState<T>.Clear()
            : NullableState<T>.Set(JsonSerializer.Deserialize(ref reader, (System.Text.Json.Serialization.Metadata.JsonTypeInfo<T>)options.GetTypeInfo(typeof(T)))!);

    public override void Write(Utf8JsonWriter writer, NullableState<T> value, JsonSerializerOptions options)
    {
        if (value.Value is null) writer.WriteNullValue();
        else JsonSerializer.Serialize(writer, value.Value, (System.Text.Json.Serialization.Metadata.JsonTypeInfo<T>)options.GetTypeInfo(typeof(T)));
    }
}

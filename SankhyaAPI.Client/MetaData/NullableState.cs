using SankhyaAPI.Client.Extensions;
using SankhyaAPI.Client.Interfaces;

namespace SankhyaAPI.Client.MetaData;

public struct NullableState<T> : INullableState
{
    public T? Value { get; set; }
    public EPropertyState State { get; set; }

    public bool HasValue => Value != null;

    public NullableState(T? value)
    {
        Value = value;
        State = value != null ? EPropertyState.Set : EPropertyState.Clear;
    }

    public static implicit operator NullableState<T>(T? value)
    {
        return new NullableState<T>(value);
    }

    public static implicit operator T?(NullableState<T> nullableState)
    {
        return nullableState.Value;
    }

    public override string ToString()
    {
        return Value?.ToString() ?? string.Empty;
    }

    public static NullableState<T> Clear()
    {
        return new NullableState<T> { State = EPropertyState.Clear };
    }
}

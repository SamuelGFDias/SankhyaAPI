namespace SankhyaAPI.Client.Envelopes;

public class SankhyaObjectKey
{
    public KeyList Key { get; set; } = new();

    public class KeyList
    {
        public List<string> Keys { get; set; } = new() { "TESTE" };
    }
}
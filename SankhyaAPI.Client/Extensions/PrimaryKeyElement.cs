namespace SankhyaAPI.Client.Extensions;

[AttributeUsage(AttributeTargets.Property)]
public class PrimaryKeyElement(string elementName) : Attribute
{
    public string ElementName => !string.IsNullOrWhiteSpace(elementName)
        ? elementName
        : throw new Exception("Elementos XML para chaves primárias não podem ser vazios");
}
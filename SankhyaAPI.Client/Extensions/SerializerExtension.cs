using System.Text;
using System.Text.Json;
using System.Xml;
using System.Xml.Serialization;
using Azure.Core.Serialization;

namespace SankhyaAPI.Client.Extensions;

public static class SerializerServiceExtension
{
    public static string XmlSerializer<T>(this T obj)
    {
        var serializer = new XmlSerializer(typeof(T));
        var settings = new XmlWriterSettings
        {
            OmitXmlDeclaration = true,
            Indent = true,
            Encoding = Encoding.UTF8
        };

        using var stringWriter = new StringWriter();
        using var xmlWriter = XmlWriter.Create(stringWriter, settings);
        serializer.Serialize(xmlWriter, obj);
        return stringWriter.ToString();
    }

    public static string XmlSerializerWithoutDeclaration<T>(this T obj)
    {
        var ns = new XmlSerializerNamespaces();
        ns.Add("", "");

        var serializer = new XmlSerializer(typeof(T));
        var settings = new XmlWriterSettings
        {
            OmitXmlDeclaration = true,
            Indent = true,
            Encoding = Encoding.UTF8
        };

        using var stringWriter = new StringWriter();
        using var xmlWriter = XmlWriter.Create(stringWriter, settings);
        serializer.Serialize(xmlWriter, obj, ns);
        return stringWriter.ToString();
    }


    public static T? XmlDeserialize<T>(this string str)
    {
        using var sr = new StringReader(str);
        return (T?)new XmlSerializer(typeof(T)).Deserialize(sr);
    }

    public static string JsonSerialize(this object obj)
    {
        var options = new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        options.Converters.Add(new NullableStateConverterFactory());

        return JsonSerializer.Serialize(obj, options);
    }


    public static T? JsonDeserialize<T>(this string str)
    {
        var serializer = new JsonObjectSerializer();
        using var sr = new MemoryStream(Encoding.UTF8.GetBytes(str));
        return (T?)serializer.Deserialize(sr, typeof(T), CancellationToken.None);
    }
}
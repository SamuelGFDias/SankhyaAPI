using System.Text;
using System.Xml.Serialization;
using Azure.Core.Serialization;
using Newtonsoft.Json;

namespace SankhyaAPI.Client.Extensions;

public static class SerializerServiceExtension
{
    public static string XmlSerializer(this object obj)
    {
        var serializer = new XmlSerializer(obj.GetType());

        using var stream = new MemoryStream();
        using var writer = new StreamWriter(stream, Encoding.UTF8);
        serializer.Serialize(writer, obj);
        writer.Flush();
        stream.Position = 0;
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    public static T? XmlDeserialize<T>(this string str)
    {
        using var sr = new StringReader(str);
        return (T?)new XmlSerializer(typeof(T)).Deserialize(sr);
    }

    public static string JsonSerialize(this object obj)
    {
        var settings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            StringEscapeHandling = StringEscapeHandling.Default
        };

        return JsonConvert.SerializeObject(obj, settings);
    }


    public static T? JsonDeserialize<T>(this string str)
    {
        var serializer = new JsonObjectSerializer();
        var sr = new MemoryStream(Encoding.UTF8.GetBytes(str));
        return (T?)serializer.Deserialize(sr, typeof(T), CancellationToken.None);
    }
}
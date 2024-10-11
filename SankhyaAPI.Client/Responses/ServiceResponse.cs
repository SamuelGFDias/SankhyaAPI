using System.Xml.Serialization;
using SankhyaAPI.Client.Envelopes;

namespace SankhyaAPI.Client.Responses;

[XmlRoot(ElementName = "serviceResponse")]
public class ServiceResponse<TEntity> : ServiceEnvelope<TEntity> where TEntity : class
{
}
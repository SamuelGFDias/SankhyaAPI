using System.Xml.Serialization;
using SankhyaAPI.Client.Envelopes;

namespace SankhyaAPI.Client.Requests;

[XmlRoot(ElementName = "serviceRequest")]
public class ServiceRequest<TEntity> : ServiceEnvelope<TEntity> where TEntity : class;
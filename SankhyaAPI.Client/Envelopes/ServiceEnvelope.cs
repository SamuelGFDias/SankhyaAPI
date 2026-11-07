using System.Text.Json.Serialization;
using System.Xml.Serialization;
using SankhyaAPI.Client.MetaData;
using SankhyaAPI.Client.Utils;

namespace SankhyaAPI.Client.Envelopes;

public abstract class ServiceEnvelope<TEntity> where TEntity : class
{
    private string? _statusMessage;
    private EServiceNames? _serviceName;
    private string? _serviceNameRecebido;

    [JsonPropertyName("serviceName")]
    [XmlAttribute(AttributeName = "serviceName")]
    public string ServiceName
    {
        get => _serviceName != null ? _serviceName.GetXmlEnumValue() : _serviceNameRecebido ?? string.Empty;
        set => _serviceNameRecebido = value;
    }

    public void SetServiceName(EServiceNames serviceNames)
    {
        _serviceName = serviceNames;
    }

    [JsonIgnore]
    [XmlAttribute(AttributeName = "outputType")]
    public string? OutputType { get; set; }

    [JsonPropertyName("status")]
    [XmlAttribute(AttributeName = "status")]
    public string? Status { get; set; }

    [JsonPropertyName("pendingPrinting")]
    [XmlAttribute(AttributeName = "pendingPrinting")]
    public string? PendingPrinting { get; set; }

    [JsonPropertyName("transactionId")]
    [XmlAttribute(AttributeName = "transactionId")]
    public string? TransactionId { get; set; }

    [JsonPropertyName("requestBody")]
    [XmlElement(ElementName = "requestBody")]
    public RequestBody RequestBody { get; set; } = new();

    [JsonPropertyName("responseBody")]
    [XmlElement(ElementName = "responseBody")]
    public ResponseBody<TEntity> ResponseBody { get; set; } = new();

    [XmlElement(ElementName = "tsError")] public TsError Error { get; set; } = new();

    [XmlElement(ElementName = "statusMessage")]
    public string? StatusMessage
    {
        get => _statusMessage;
        set
        {
            if (value != null) _statusMessage = SankhyaText.DecodeBase64(value);
        }
    }
}

public class TsError
{
    [XmlAttribute(AttributeName = "tsErrorCode")]
    public string? TsErrorCode { get; set; }

    [XmlAttribute(AttributeName = "tsErrorLevel")]
    public string? TsErrorLevel { get; set; }

    private string? _statusMessage;

    [XmlElement(ElementName = "statusMessage")]
    public string? StatusMessage
    {
        get => _statusMessage;
        set => _statusMessage = SankhyaText.DecodeBase64(value);
    }
}
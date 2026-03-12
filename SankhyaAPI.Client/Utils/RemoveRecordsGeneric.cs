using System.Reflection;
using System.Xml.Linq;
using SankhyaAPI.Client.Envelopes;
using SankhyaAPI.Client.Extensions;
using SankhyaAPI.Client.MetaData;
using SankhyaAPI.Client.Requests;

namespace SankhyaAPI.Client.Utils;

public static class RemoveRecordsGeneric
{
    public static ServiceRequest<T> CreateRemoveEnvelope<T>(List<T> objs, string entityName)
        where T : class, new()
    {
        ObjectUtilsMethods.ValidarCamposChave(objs, true);

        var entity = new XElement("entity", new XAttribute("rootEntity", entityName));

        foreach (T obj in objs)
        {
            var keys = new XElement("id");
            keys.AddKeysAsXml(obj);
            entity.Add(keys);
        }

        var envelope = new ServiceRequest<T> { RequestBody = new RequestBody { Entity = entity } };
        envelope.SetServiceName(EServiceNames.RemoveRecords);
        return envelope;
    }
}
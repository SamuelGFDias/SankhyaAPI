using System.Xml.Serialization;

namespace SankhyaAPI.Client.MetaData;

public enum EServiceNames
{
    [XmlEnum("DbExplorerSP.executeQuery")] ExecuteQuery,
    [XmlEnum("MobileLoginSP.login")] Login,
    [XmlEnum("CRUDServiceProvider.loadRecords")] LoadRecords,
    [XmlEnum("CRUDServiceProvider.saveRecord")] SaveRecords,
    [XmlEnum("VisualizadorRelatorios.visualizarRelatorio")] VisualizadorRelatorios,
    [XmlEnum("CRUDServiceProvider.removeRecords")] RemoveRecords
}
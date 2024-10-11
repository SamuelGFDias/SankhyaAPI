namespace SankhyaAPI.Client.MetaData;

public static class ServiceNames
{
    public enum ServiceName
    {
        DbExplorerSpExecuteQuery,
        MobileLoginSpLogin,
        CrudServiceProviderLoadRecords,
        CrudServiceProviderSaveRecords,
        VisualizadorRelatorios
    }

    public const string DbExplorerSpExecuteQuery = "DbExplorerSP.executeQuery";
    public const string MobileLoginSpLogin = "MobileLoginSP.login";
    public const string CrudServiceProviderLoadRecords = "CRUDServiceProvider.loadRecords";
    public const string CrudServiceProviderSaveRecords = "CRUDServiceProvider.saveRecord";
    public const string VisualizadorRelatorios = "VisualizadorRelatorios.visualizarRelatorio";
}
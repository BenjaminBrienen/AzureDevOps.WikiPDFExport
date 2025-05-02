namespace AzureDevOps.WikiPDFExport;

[System.Serializable]
internal class WikiPDFExportException : System.Exception
{
	public WikiPDFExportException() { }
	public WikiPDFExportException(string message) : base(message) { }
	public WikiPDFExportException(string message, System.Exception inner) : base(message, inner) { }
}

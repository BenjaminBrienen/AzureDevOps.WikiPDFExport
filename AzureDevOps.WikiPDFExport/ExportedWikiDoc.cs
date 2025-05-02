using System.IO;

namespace AzureDevOps.WikiPDFExport;

/// <summary>
/// Information about an export from an Azure Devops Wiki
/// </summary>
internal record ExportedWikiDoc
{
	private ExportedWikiDoc(DirectoryInfo exportDirectory, DirectoryInfo baseDirectory)
	{
		ExportDirectory = exportDirectory;
		BaseDirectory = baseDirectory;
	}

	/// <summary>
	/// Create a wiki export from a path to the base of the export (not the wiki).
	/// </summary>
	/// <param name="exportBase">base directory to export to</param>
	public static ExportedWikiDoc? New(DirectoryInfo exportBase)
	{
		if (!exportBase.Exists)
		{
			return null;
		}
		var inferredBaseDirectory = FindNearestParentAttachmentsDirectory(exportBase);
		return new ExportedWikiDoc(exportBase, inferredBaseDirectory ?? exportBase);
	}

	/// <summary>
	/// Create a wiki export from a path to the base of the export (not the wiki).
	/// </summary>
	/// <param name="exportBase">base directory to export to</param>
	public static ExportedWikiDoc? New(string exportBase)
	{
		var directoryInfo = new DirectoryInfo(exportBase);
		return New(directoryInfo);
	}

	/// <summary>
	/// Search from the given existing directory upwards until a folder containing
	/// an 'attachment' folder is identified.
	/// </summary>
	/// <param name="exportBase">base directory to export to</param>
	/// <returns>
	/// A valid existing directory which contains an attachments folder.
	/// </returns>
	/// <throws>
	/// WikiPDFExportException if an attachments folder is not found before hitting the root.
	/// </throws>
	public static DirectoryInfo? FindNearestParentAttachmentsDirectory(DirectoryInfo exportBase)
	{
		if (exportBase.GetDirectories(".attachments", SearchOption.TopDirectoryOnly) is not [])
		{
			return exportBase;
		}
		if (exportBase.Parent is null)
		{
			return null;
		}
		return FindNearestParentAttachmentsDirectory(exportBase.Parent);
	}

	public DirectoryInfo ExportDirectory { get; }
	public DirectoryInfo BaseDirectory { get; }

	public string ExportPath()
	{
		return ExportDirectory.FullName;
	}

	public string BasePath()
	{
		return BaseDirectory.FullName;
	}
}

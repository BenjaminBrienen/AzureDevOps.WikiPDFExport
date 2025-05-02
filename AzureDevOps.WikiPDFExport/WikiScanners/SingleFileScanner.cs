using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace AzureDevOps.WikiPDFExport;

internal class SingleFileScanner(
	string filePath,
	string wikiPath,
	ILogger logger
)
{
	private readonly string singleFilePath = filePath;

	/// <summary>
	/// to compute relative path
	/// </summary>
	private readonly string _basePath = new DirectoryInfo(Path.GetFullPath(wikiPath)).FullName;

	public List<MarkdownFile> Scan()
	{
		// handle the case that we have a .order file
		var allFiles = ScanInner();

		// handle the case that we have a single file only by providing a .md file in the -s parameter
		if (singleFilePath.EndsWith(".md", true, System.Globalization.CultureInfo.InvariantCulture))
		{
			var fileInfo = new FileInfo(singleFilePath);
			var file = new MarkdownFile(fileInfo, string.Empty, 0, Path.GetRelativePath(wikiPath, fileInfo.FullName));
			return [file];
		}

		var current = allFiles.FirstOrDefault(m => m.FileRelativePath.Contains(singleFilePath, StringComparison.Ordinal));

		if (current is null || (allFiles.IndexOf(current) is var index && index == -1))
		{
			logger.Log($"Single-File [-s] {singleFilePath} specified not found" + singleFilePath, LogLevel.Error);
			throw new ArgumentException($"{singleFilePath} not found");
		}
		var result = new List<MarkdownFile> { current };

		if (allFiles.Count > index)
		{
			foreach (var item in allFiles.Skip(index + 1))
			{
				if (item.Level > current.Level)
				{
					result.Add(item);
				}
				else
				{
					break;
				}
			}
		}
		return result;
	}

	public List<MarkdownFile> ScanInner()
	{
		return ReadOrderFiles(wikiPath, 0);
	}

	private List<MarkdownFile> ReadOrderFiles(string path, int level)
	{
		// Read the .order file.
		// If there is an entry and a folder with the same name, dive deeper.
		var directory = new DirectoryInfo(Path.GetFullPath(path));
		Log($"Reading .order file in directory {path}");
		var orderFiles = directory.GetFiles(".order", SearchOption.TopDirectoryOnly);

		if (orderFiles.Length > 0)
		{
			Log("Order file found", LogLevel.Debug, 1);
		}

		var result = new List<MarkdownFile>();
		foreach (var orderFile in orderFiles)
		{
			if (orderFile.Directory is null)
			{
				throw new UnreachableException($"The file should be {path}.");
			}
			var orders = File.ReadAllLines(orderFile.FullName);
			Log($"Pages: {orders.Length}", LogLevel.Information, 2);
			var relativePath = orderFile.Directory.FullName.Length > directory.FullName.Length
				? orderFile.Directory.FullName[directory.FullName.Length..]
				: "/";

			foreach (var order in orders)
			{
				// skip empty lines
				if (string.IsNullOrEmpty(order))
				{
					Log("Skipped empty line", LogLevel.Debug, 3);
					continue;
				}

				var absolutePath = Path.Combine(orderFile.Directory.FullName, $"{order}.md");
				var mf = new MarkdownFile(
					new FileInfo(absolutePath),
					$"{relativePath}",
					level,
					absolutePath[_basePath.Length..].Replace('\\', '/'));
				result.Add(mf);
				Log($"Adding page: {mf.AbsolutePath}", LogLevel.Information, 2);
				var childPath = Path.Combine(orderFile.Directory.FullName, order);
				if (Directory.Exists(childPath))
				{
					result.AddRange(ReadOrderFiles(childPath, level + 1));
				}
			}
		}
		return result;
	}

	public void Log(string message, LogLevel logLevel = LogLevel.Information, int indent = 0)
	{
		logger.Log(message, logLevel, indent);
	}
}

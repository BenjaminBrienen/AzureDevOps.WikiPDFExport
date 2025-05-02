using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace AzureDevOps.WikiPDFExport;

internal class WikiDirectoryScanner(string wikiPath, Options options, ILogger logger) : ILogger
{
	/// <summary>
	/// to compute relative path
	/// </summary>
	private readonly string _basePath = new DirectoryInfo(Path.GetFullPath(wikiPath)).FullName;

	public List<MarkdownFile> Scan()
	{
		var excludeRegexes = options.ExcludePaths
			.Select(exclude => new Regex($".*{exclude}.*", RegexOptions.IgnoreCase))
			.ToList();
		return ReadPagesInOrderImpl(wikiPath, 0, excludeRegexes);
	}

	private List<MarkdownFile> ReadPagesInOrderImpl(string path, int level, ICollection<Regex> excludeRegexes)
	{
		Debug.Print($"{level} scanning {path}");

		var result = new List<MarkdownFile>();

		var directory = new DirectoryInfo(Path.GetFullPath(path));
		Log($"Reading .md files in directory {path}");
		var pages = directory.GetFiles("*.md", SearchOption.TopDirectoryOnly).ToList();
		Log($"Total pages in dir: {pages.Count}", LogLevel.Information, 1);

		var subDirs = directory.GetDirectories("*", SearchOption.TopDirectoryOnly)
			.SkipWhile(d => d.Name.StartsWith('.'))
			.ToList();

		subDirs.Sort(static (a, b) => string.CompareOrdinal(a.FullName, b.FullName));
		pages.Sort(static (a, b) => string.CompareOrdinal(a.FullName, b.FullName));

		Log($"Reading .order file in directory {path}");
		var orderFile = Path.Combine(directory.FullName, ".order");
		var ordered = File.Exists(orderFile) ? ReorderPages(pages, orderFile) : pages;
		foreach (var page in ordered)
		{
			var pageRelativePath = page.FullName[_basePath.Length..].Replace('\\', '/');

			var mf = new MarkdownFile(page, "/", level, pageRelativePath);
			if (mf.PartialMatches(excludeRegexes))
			{
				Log($"Skipping page: {mf.AbsolutePath}", LogLevel.Information, 2);
			}
			else
			{
				result.Add(mf);
				Log($"Adding page: {mf.AbsolutePath}", LogLevel.Information, 2);
			}

			var matchingDir = subDirs.FirstOrDefault(
				d => string.Equals(d.Name, Path.GetFileNameWithoutExtension(page.Name), System.StringComparison.OrdinalIgnoreCase));
			if (matchingDir is not null)
			{
				// depth first recursion to be coherent with previous versions
				result.AddRange(
					ReadPagesInOrderImpl(matchingDir.FullName, level + 1, excludeRegexes));
				_ = subDirs.Remove(matchingDir);
			}
		}

		// any leftover
		result.AddRange(
			subDirs.SelectMany(d => ReadPagesInOrderImpl(d.FullName, level + 1, excludeRegexes)));
		return result;
	}

	private List<FileInfo> ReorderPages(ICollection<FileInfo> pages, string orderFile)
	{
		var result = new List<FileInfo>();

		Log("Order file found", LogLevel.Debug, 1);
		var orders = File.ReadAllLines(Path.GetFullPath(orderFile));
		Log($"Pages in order: {orders.Length}", LogLevel.Information, 2);

		// sort pages according to order file
		// NOTE some may not match or partially match
		foreach (var order in orders)
		{
			// TODO linear search... not very optimal
			var page = pages.FirstOrDefault(
				page => string.Equals(Path.GetFileNameWithoutExtension(page.Name), order, System.StringComparison.OrdinalIgnoreCase));
			if (page is not null)
			{
				result.Add(page);
			}
		}
		var notInOrderFile = pages.Except(result);
		result.AddRange(notInOrderFile);
		return result;
	}

	public void Log(string message, LogLevel logLevel = LogLevel.Information, int indent = 0)
	{
		logger.Log(message, logLevel, indent);
	}
}

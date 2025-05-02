using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using AzureDevOps.WikiPDFExport.MermaidContainer;
using Markdig;
using Markdig.Extensions.Yaml;
using Markdig.Helpers;
using Markdig.Parsers;
using Markdig.Renderers;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Microsoft.Extensions.Logging;
using Microsoft.TeamFoundation.Common;
using Microsoft.VisualStudio.Services.Common;

namespace AzureDevOps.WikiPDFExport;

internal partial class MarkdownConverter : ILogger
{
	private readonly ILogger _logger;
	private readonly Options _options;
	private readonly ExportedWikiDoc _wiki;

	internal MarkdownConverter(ExportedWikiDoc wiki, Options options, ILogger logger)
	{
		_wiki = wiki;
		_options = options;
		_logger = logger;
	}

	internal string ConvertToHtml(IList<MarkdownFile> files)
	{
		Log("Converting Markdown to HTML");
		var sb = new StringBuilder();

		// setup the markdown pipeline to support tables
		var pipelineBuilder = new MarkdownPipelineBuilder()
			.UsePipeTables()
			.UseEmojiAndSmiley()
			.UseAdvancedExtensions()
			.UseYamlFrontMatter()
			.UseTableOfContent(
				tocAction: opt => { opt.ContainerTag = "div"; opt.ContainerClass = "toc"; }
			);

		// must be handled by us to have linking across files
		_ = pipelineBuilder.Extensions.RemoveAll(x => x is Markdig.Extensions.AutoIdentifiers.AutoIdentifierExtension);
		// handled by katex
		_ = pipelineBuilder.Extensions.RemoveAll(x => x is Markdig.Extensions.Mathematics.MathExtension);

		// todo: is this needed? it will stop support of resizing images:
		// this interferes with katex parsing of {} elements.
		// pipelineBuilder.Extensions.RemoveAll(x => x is Markdig.Extensions.GenericAttributes.GenericAttributesExtension);

		var deeplink = new DeepLinkExtension(DeepLinkOptions.Default);
		pipelineBuilder.Extensions.Add(deeplink);

		if (_options.ConvertMermaid)
		{
			pipelineBuilder = pipelineBuilder.UseMermaidContainers();
		}

		HandleGlobalToc(files);
		BuildExport(files, sb, pipelineBuilder, deeplink);
		return sb.ToString();
	}

	private void BuildExport(IList<MarkdownFile> files, StringBuilder sb, MarkdownPipelineBuilder pipelineBuilder, DeepLinkExtension deeplink)
	{
		for (var i = 0; i < files.Count; i++)
		{
			var mf = files[i];
			var file = mf.FileInfo;

			Log($"{file.Name}", LogLevel.Information, 1);

			var md = mf.Content;

			if (string.IsNullOrEmpty(md))
			{
				Log($"File {file.FullName} is empty and will be skipped!", LogLevel.Warning, 1);
				continue;
			}

			// rename TOC tags to fit to MarkdigToc or delete them from each markdown document
			var newTocString = _options.GlobalToc is not null ? string.Empty : "[TOC]";
			md = md.Replace("[[_TOC_]]", newTocString, StringComparison.Ordinal);

			// remove scalings from image links, width & height: file.png =600x500
			var regexImageScalings = @"\((.[^\)]*?[png|jpg|jpeg]) =(\d+)x(\d+)\)";
			md = Regex.Replace(md, regexImageScalings, "($1){width=$2 height=$3}");

			// remove scalings from image links, width only: file.png =600x
			regexImageScalings = @"\((.[^\)]*?[png|jpg|jpeg]) =(\d+)x\)";
			md = Regex.Replace(md, regexImageScalings, "($1){width=$2}");

			// remove scalings from image links, height only: file.png =x600
			regexImageScalings = @"\((.[^\)]*?[png|jpg|jpeg]) =x(\d+)\)";
			md = Regex.Replace(md, regexImageScalings, "($1){height=$2}");

			// determine the correct nesting of pages and related chapters
			_ = pipelineBuilder.BlockParsers.Replace<HeadingBlockParser>(new OffsetHeadingBlockParser(mf.Level + 1));

			// update the deeplinking
			deeplink.Filename = Path.GetFileNameWithoutExtension(file.FullName);

			var pipeline = pipelineBuilder.Build();

			// parse the markdown document so we can alter it later
			var document = Markdown.Parse(md, pipeline);

			if (_options.NoFrontmatter)
			{
				RemoveFrontmatter(document);
			}

			if (!string.IsNullOrEmpty(_options.Filter))
			{
				if (!PageMatchesFilter(document))
				{
					Log("Page does not have correct tags - skip", LogLevel.Information, 3);
					continue;
				}

				Log("Page tags match the provided filter", LogLevel.Information, 3);
			}

			// adjust the links
			CorrectLinksAndImages(document, mf);

			var builder = new StringBuilder();
			using (var writer = new StringWriter(builder))
			{
				// write the HTML output
				var renderer = new HtmlRenderer(writer);
				pipeline.Setup(renderer);
				_ = renderer.Render(document);
			}
			var html = builder.ToString();

			if (!string.IsNullOrEmpty(_options.GlobalToc) && i == _options.GlobalTocPosition)
			{
				html = RemoveDuplicatedHeadersFromGlobalToc(html);
				Log("Removed duplicated headers from toc html", LogLevel.Information, 1);
			}

			// TODO how is this different from data in MarkdownFile?
			// add html anchor
#pragma warning disable CA1308 // Intentional
			var anchorPath = file.FullName[_wiki.ExportPath().Length..]
				.Replace("\\", string.Empty, StringComparison.Ordinal)
				.Replace("/", string.Empty, StringComparison.Ordinal)
				.ToLowerInvariant()
				.Replace(".md", string.Empty, StringComparison.Ordinal);
#pragma warning restore CA1308
			var relativePath = file.FullName[_wiki.ExportPath().Length..];

			var anchor = $"<a id=\"{anchorPath}\">&nbsp;</a>";

			Log($"Anchor: {anchorPath}", LogLevel.Information, 3);

			html = anchor + html;

			if (_options.PathToHeading)
			{
				var filename = HttpUtility.UrlDecode(relativePath);
				var heading = $"<b>{filename}</b>";
				html = heading + html;
			}

			if (!string.IsNullOrEmpty(_options.GlobalToc) && i == _options.GlobalTocPosition && !_options.Heading)
			{
				var heading = $"<h1>{_options.GlobalToc}</h1>";
				html = heading + html;
			}
			// TODO add page numbers
			if (_options.Heading)
			{
				var filename = file.Name.Replace(".md", string.Empty, StringComparison.Ordinal);
				filename = HttpUtility.UrlDecode(filename);
				var filenameEscapes = new Dictionary<string, string>
				{
					{"%3A", ":"},
					{"%3C", "<"},
					{"%3E", ">"},
					{"%2A", "*"},
					{"%3F", "?"},
					{"%7C", "|"},
					{"%2D", "-"},
					{"%22", "\""},
					{"-", " "},
				};

				var title = new StringBuilder(filename);
				foreach (var filenameEscape in filenameEscapes)
				{
					_ = title.Replace(filenameEscape.Key, filenameEscape.Value);
				}

				var heading = $"<h{mf.Level + 1}>{title}</h{mf.Level + 1}>";
				html = heading + html;
			}

			if (_options.BreakPage)
			{
				// add break at the end of each page except the last one
				if (i + 1 < files.Count)
				{
					Log("----------------------", LogLevel.Information);
					Log("Adding new page to PDF", LogLevel.Information);
					Log("----------------------", LogLevel.Information);
					html = $"<div style='page-break-after: always;'>{html}</div>";
				}
			}

			if (_options.Debug)
			{
				Log($"html:\n{html}", LogLevel.Debug, 1);
			}
			_ = sb.Append(html);
		}
	}

	private void HandleGlobalToc(IList<MarkdownFile> files)
	{
		if (string.IsNullOrEmpty(_options.GlobalToc))
		{
			return;
		}

		if (_options.GlobalTocPosition > files.Count)
		{
			_options.GlobalTocPosition = files.Count;
		}

		var firstMarkdownFileInfo = files[_options.GlobalTocPosition].FileInfo;
		if (firstMarkdownFileInfo.Directory is null)
		{
			throw new UnreachableException("Files are always in directories.");
		}
		var directoryName = firstMarkdownFileInfo.Directory.Name;
		var tocName = string.IsNullOrEmpty(_options.GlobalToc) ? directoryName : _options.GlobalToc;
		var relativePath = $"{tocName}.md";
		var tocMarkdownFilePath = Path.Join(firstMarkdownFileInfo.DirectoryName, relativePath);

		var contents = files.Select(x => x.Content).ToList();
		var tocContent = CreateGlobalTableOfContent(contents);
		var tocString = string.Join("\n", tocContent);
		var tocMarkdownFile = new MarkdownFile(new FileInfo(tocMarkdownFilePath), relativePath, 0, relativePath, tocString);

		files.Insert(_options.GlobalTocPosition, tocMarkdownFile);
	}

	internal static List<string> CreateGlobalTableOfContent(List<string> contents)
	{
		var headers = new List<string>();
		foreach (var content in RemoveCodeSections(contents))
		{
			var headerMatches = MarkdownHeader().Matches(content);
			headers.AddRange(headerMatches.Select(x => x.Value.Trim()));
		}

		if (headers.Count == 0)
		{
			return []; // no header -> no toc
		}

		var tocContent = new List<string> { "[TOC]" }; // MarkdigToc style
		tocContent.AddRange(headers);
		return tocContent;
	}

	private static List<string> RemoveCodeSections(List<string> contents)
	{
		var contentWithoutCode = new List<string>();
		foreach (var content in contents)
		{
			var contentWithoutCodeSection = MarkdownCodeBlock().Replace(content, string.Empty);
			contentWithoutCode.Add(contentWithoutCodeSection);
		}
		return contentWithoutCode;
	}

	private void RemoveFrontmatter(MarkdownDocument document)
	{
		if (document.Descendants<YamlFrontMatterBlock>().FirstOrDefault() is not YamlFrontMatterBlock frontmatter)
		{
			return;
		}
		_ = document.Remove(frontmatter);
		Log("Removed Frontmatter/Yaml tags", LogLevel.Information, 1);
	}

	private bool PageMatchesFilter(MarkdownObject document)
	{
		if (string.IsNullOrEmpty(_options.Filter))
		{
			return true;
		}

		Log($"Filter provided: {_options.Filter}", LogLevel.Information, 2);

		var filters = _options.Filter.Split(",");
		var frontmatter = document.Descendants<YamlFrontMatterBlock>().FirstOrDefault();

		if (frontmatter is null)
		{
			Log("Page has no frontmatter tags", LogLevel.Information, 2);
			return false;
		}

		var frontmatterTags = new List<string>();
		var lastTag = string.Empty;
		foreach (var frontmatterline in frontmatter.Lines.Cast<StringLine>())
		{
			var splice = frontmatterline.Slice.ToString();
			var split = splice.Split(":");

			// title:test or <empty>:test2 or tags:<empty>
			if (split.Length == 2)
			{
				// title:
				if (string.IsNullOrEmpty(split[1]))
				{
					lastTag = split[0].Trim();
				}
				// title:test
				else if (!string.IsNullOrEmpty(split[0]))
				{
					frontmatterTags.Add($"{split[0].Trim()}:{split[1].Trim()}");
				}
				//:test2
				else
				{
					frontmatterTags.Add($"{lastTag}:{split[1].Trim()}");
				}
			}
			else if (split.Length == 1 && !string.IsNullOrEmpty(split[0]))
			{
				frontmatterTags.Add($"{lastTag}:{split[0].Trim()[2..]}");
			}
		}

		foreach (var filter in filters)
		{
			if (frontmatterTags.Contains(filter, StringComparer.OrdinalIgnoreCase))
			{
				return true;
			}
		}
		return false;
	}

	public void CorrectLinksAndImages(MarkdownObject document, MarkdownFile mf)
	{
		Log("Correcting Links and Images", LogLevel.Information, 2);
		var file = mf.FileInfo;
		// walk the document node tree and replace relative image links
		// and relative links to markdown pages
		foreach (var link in document.Descendants().OfType<LinkInline>())
		{
			if (link.Url?.StartsWith("http", StringComparison.Ordinal) == false)
			{
				string? absolutePath;
				string? anchor = null;
				var isBase64 = false;

				// handle --attachments-path case
				if (!string.IsNullOrEmpty(this._options.AttachmentsPath)
					&& (link.Url.StartsWith("/.attachments", StringComparison.Ordinal) || link.Url.StartsWith(".attachments", StringComparison.Ordinal)))
				{
					var linkUrl = link.Url.Split('/').Last();

					//urls could be encoded and contain spaces - they are then not found on disk
					linkUrl = HttpUtility.UrlDecode(linkUrl);

					absolutePath = Path.GetFullPath(Path.Combine(this._options.AttachmentsPath, linkUrl));
				}
				else if (link.Url.StartsWith('/'))
				{
					// Add URL replacements here
					var replacements = new Dictionary<string, string>
					{
						{":", "%3A"},
						{"#", "-"},
						{"%20", " "},
					};
					var linkUrl = link.Url;
					replacements.ForEach(p => linkUrl = linkUrl.Replace(p.Key, p.Value, StringComparison.Ordinal));
					var basePath = _wiki.BasePath();
					absolutePath = Path.GetFullPath(Path.Join(basePath, linkUrl));
				}
				else if (link.Url.StartsWith("data:", StringComparison.Ordinal))
				{
					isBase64 = true;
					absolutePath = null;
				}
				else
				{
					var split = link.Url.Split("#");
					var linkUrl = split[0];
					anchor = split.Length > 1 ? split[1] : null;
					if (file.Directory is null)
					{
						throw new UnreachableException("Files are always in directories.");
					}
					absolutePath = Path.GetFullPath(Path.Join(file.Directory.FullName, linkUrl));
				}

				if (!Path.Exists(absolutePath))
				{
					Log($"Invalid link '{absolutePath}'", LogLevel.Warning, 2);
				}

				// the file is a markdown file, create a link to it
				var isMarkdown = !isBase64
					&& NewMethod(link, absolutePath ?? throw new UnreachableException("This is always set if isBase64 is false."));

				// only markdown files get a pdf internal link
				if (isMarkdown)
				{
					var relativePath = MakePDFRelativeInternalLink(mf.RelativePath, link.Url, _wiki.ExportPath(), _wiki.BasePath());
					// Log the final result to help with debugging or verifying link correctness during export
					Log($"Markdown link: {relativePath}", LogLevel.Debug, 2);
					link.Url = $"#{relativePath}";
				}
			}
			CorrectLinksAndImages(link, mf);
		}
	}

	private static bool NewMethod(LinkInline link, string absolutePath)
	{
		var isMarkdown = false;
		var fileInfo = new FileInfo(absolutePath);
		if (fileInfo.Exists && fileInfo.Extension.Equals(".md", StringComparison.OrdinalIgnoreCase))
		{
			isMarkdown = true;
		}
		else if (fileInfo.Exists)
		{
			//convert images to base64 and embed them in the html. Chrome/Puppeter does not show local files because of security reasons.
			var bytes = File.ReadAllBytes(fileInfo.FullName);
			var base64 = Convert.ToBase64String(bytes);
			link.Url = $"data:image/{(fileInfo.Extension == ".svg" ? "svg+xml" : fileInfo.Extension)};base64,{base64}";
		}

		fileInfo = new FileInfo($"{absolutePath}.md");
		if (fileInfo.Exists && fileInfo.Extension.Equals(".md", StringComparison.OrdinalIgnoreCase))
		{
			isMarkdown = true;
		}
		return isMarkdown;
	}

	private static string MakePDFRelativeInternalLink(string relativePath, string url, string exportPath, string basePath)
	{
		// Start by combining the relative path of the current Markdown file with the link's URL
		// This constructs the initial path to the target file as if it were being referenced locally
		var fixedPath = relativePath + "\\" + url;

		// Remove any anchor fragments (e.g., "#section") from the URL
		// PDF links don't support in-document anchors the same way HTML does, so this avoids broken links
		fixedPath = fixedPath.Split("#")[0];

		// Normalize directory separators to backslashes to maintain Windows file path compatibility
		fixedPath = fixedPath.Replace("/", "\\", StringComparison.Ordinal);

		// Adjust the path to be relative to the export path instead of the base wiki path
		// This ensures internal links are correct even if the export doesn't start from the wiki root
		fixedPath = MakePathRelative(exportPath, fixedPath, basePath);

		// Remove all backslashes to convert the path into a flat structure suitable for PDF linking
		// PDF internal links often use simplified paths/names without folder separators
		fixedPath = fixedPath.Replace("\\", string.Empty, StringComparison.Ordinal);

		// Strip the ".md" extension since PDFs typically reference section names or page IDs, not source files
		fixedPath = fixedPath.Replace(".md", string.Empty, StringComparison.Ordinal);

#pragma warning disable CA1308 // Normalize casing for predictable comparison and consistency
		// Convert to lowercase for uniformity and to avoid case-sensitivity issues in downstream tooling
		fixedPath = fixedPath.ToLowerInvariant();
#pragma warning restore CA1308
		return fixedPath;
	}

	private static string MakePathRelative(string exportPath, string relativePath, string basePath)
	{
		var pathBelowRootWiki = exportPath.Replace(basePath, string.Empty, StringComparison.Ordinal);
		if (!pathBelowRootWiki.IsNullOrEmpty())
		{
			relativePath = relativePath.Replace(pathBelowRootWiki, string.Empty, StringComparison.Ordinal);
		}
		return relativePath;
	}

	internal static string RemoveDuplicatedHeadersFromGlobalToc(string html)
	{
		return HtmlHeader()
			.Replace(html, string.Empty)
			.Trim('\n');
	}

	public void Log(string message, LogLevel logLevel = LogLevel.Information, int indent = 0)
	{
		_logger.Log(message, logLevel, indent);
	}

	// private static string GetAttachmentsPathLocalFile(FileInfo file, string url)
	// {
	// 	if (file.Directory is null)
	// 	{
	// 		throw new UnreachableException("Files are always in directories.");
	// 	}
	// 	var withoutFragment = url.Split("#")[0];
	// 	var relativePath = Path.Combine(file.Directory.FullName, withoutFragment);
	// 	var absolutePath = Path.GetFullPath(relativePath);
	// 	return absolutePath;
	// }

	// private string GetAttachmentsPathRoot(LinkInline link)
	// {
	// 	// Add URL replacements here
	// 	var replacements = new Dictionary<string, string>{
	// 						{":", "%3A"},
	// 						{"#", "-"},
	// 						{"%20", " "},
	// 					};
	// 	foreach (var replacement in replacements)
	// 	{
	// 		if (link.Url is null)
	// 		{
	// 			throw new UnreachableException("This is always set.");
	// 		}
	// 		link.Url = link.Url.Replace(replacement.Key, replacement.Value, StringComparison.Ordinal);
	// 	}
	// 	if (link.Url is null)
	// 	{
	// 		throw new UnreachableException("This is always set.");
	// 	}
	// 	var relativePath = Path.Combine(_wiki.BasePath(), link.Url);
	// 	var absolutePath = Path.GetFullPath(relativePath);
	// 	return absolutePath;
	// }

	// private string GetAttachmentsPathRaw(string url)
	// {
	// 	var linkUrl = url.Split('/').Last();

	// 	// URLs could be encoded and contain spaces - they are then not found on disk
	// 	linkUrl = HttpUtility.UrlDecode(linkUrl);

	// 	if (this._options.AttachmentsPath is null)
	// 	{
	// 		throw new UnreachableException("This is always set.");
	// 	}
	// 	var relativePath = Path.Combine(this._options.AttachmentsPath, linkUrl);
	// 	var absolutePath = Path.GetFullPath(relativePath);
	// 	return absolutePath;
	// }

	// private static bool IsData(string uri)
	// {
	// 	return uri.StartsWith("data:", StringComparison.Ordinal);
	// }

	// private static bool IsRoot(string uri)
	// {
	// 	return uri.StartsWith('/');
	// }

	// private bool IsAttachments(string uri)
	// {
	// 	return !string.IsNullOrEmpty(this._options.AttachmentsPath)
	// 	&& (uri.StartsWith("/.attachments", StringComparison.Ordinal) || uri.StartsWith(".attachments", StringComparison.Ordinal));
	// }

	/// <summary>
	/// Returns a compiled regular expression that matches full HTML header tags (h1 through h6)
	/// on individual lines. The pattern is useful for detecting lines that contain only an HTML
	/// header element and nothing else.
	///
	/// <para>
	/// Pattern explanation:
	/// <code>
	/// ^ *              # Start of line, followed by any number of spaces
	/// <h[123456].*>    # Opening HTML header tag: <h1>, <h2>, ..., <h6> with optional attributes
	/// .*               # Any content inside the header (non-greedy match not enforced)
	/// </h[123456]>     # Closing HTML header tag matching the same number
	/// *\n?             # Optional trailing whitespace and optional single newline
	/// $                # End of line
	/// </code>
	/// </para>
	///
	/// <remarks>
	/// This regex does not enforce that the opening and closing header tags are for the same level.
	/// </remarks>
	/// </summary>
	[GeneratedRegex(@"^ *<h[123456].*>.*<\/h[123456]> *\n?$", RegexOptions.Multiline)]
	private static partial Regex HtmlHeader();

	/// <summary>
	/// Returns a compiled regular expression that matches Markdown-style headers using
	/// hash notation (e.g., <c># Header</c>, <c>## Subheader</c>) at the start of a line.
	///
	/// <para>
	/// Pattern explanation:
	/// <code>
	/// ^ *         # Start of line, followed by any number of spaces
	/// #+          # One or more '#' symbols (for header level 1 to 6)
	/// ?           # Optional single space after the hashes
	/// [^#].*      # Ensure the header is not empty or made entirely of '#' (must contain content)
	/// $           # End of line
	/// </code>
	/// </para>
	/// <remarks>
	/// This regex excludes "setext-style" headers (underlines with === or ---) and ensures that
	/// header content exists by checking the first character after the hashes is not another hash.
	/// </remarks>
	/// </summary>
	[GeneratedRegex("^ *#+ ?[^#].*$", RegexOptions.Multiline)]
	private static partial Regex MarkdownHeader();

	/// <summary>
	/// Returns a compiled regular expression that matches Markdown code block delimiters
	/// using backticks (```), or tildes (~~~), on lines by themselves. This can be used to identify
	/// fenced code blocks in Markdown syntax.
	///
	/// <para>
	/// Pattern explanation:
	/// <code>
	/// ^[ \t]*            # Start of line with optional leading spaces or tabs
	/// (```|~~~)          # Opening code block fence (either three backticks or three tildes)
	/// [^`]*              # Optional non-backtick content after the fence (e.g., language identifier)
	/// (```|~~~)          # Closing fence, expected on the same line (nonstandard, but allowed)
	/// </code>
	/// </para>
	/// </summary>
	[GeneratedRegex("^[ \t]*(```|~~~)[^`]*(```|~~~)", RegexOptions.Multiline)]
	private static partial Regex MarkdownCodeBlock();
}

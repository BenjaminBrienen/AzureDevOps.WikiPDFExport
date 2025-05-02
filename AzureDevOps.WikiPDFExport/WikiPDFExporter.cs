using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Microsoft.VisualStudio.Services.Client;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;
using Process = System.Diagnostics.Process;

[assembly: InternalsVisibleTo("AzureDevOps.WikiPDFExport.Test")]

namespace AzureDevOps.WikiPDFExport;

internal partial class WikiPDFExporter : ILogger
{
	private readonly ILoggerExtended _logger;
	private readonly Options _options;
	private readonly Dictionary<string, string> _iconClass;

	public WikiPDFExporter(Options options, ILoggerExtended logger)
	{
		_options = options;
		_logger = logger;

		this._iconClass = new Dictionary<string, string>{
			{"icon_crown", "bowtie-symbol-crown"},
			{"icon_trophy", "bowtie-symbol-trophy"},
			{"icon_list", "bowtie-symbol-list"},
			{"icon_book", "bowtie-symbol-book"},
			{"icon_sticky_note", "bowtie-symbol-stickynote"},
			{"icon_clipboard", "bowtie-symbol-task"},
			{"icon_insect", "bowtie-symbol-bug"},
			{"icon_traffic_cone", "bowtie-symbol-impediment"},
			{"icon_chat_bubble", "bowtie-symbol-review"},
			{"icon_flame", "bowtie-symbol-flame"},
			{"icon_megaphone", "bowtie-symbol-ask"},
			{"icon_test_plan", "bowtie-test-plan"},
			{"icon_test_suite", "bowtie-test-suite"},
			{"icon_test_case", "bowtie-test-case"},
			{"icon_test_step", "bowtie-test-step"},
			{"icon_test_parameter", "bowtie-test-parameter"},
			{"icon_code_review", "bowtie-symbol-review-request"},
			{"icon_code_response", "bowtie-symbol-review-response"},
			{"icon_review", "bowtie-symbol-feedback-request"},
			{"icon_response", "bowtie-symbol-feedback-response"},
			{"icon_ribbon", "bowtie-symbol-ribbon"},
			{"icon_chart", "bowtie-symbol-finance"},
			{"icon_headphone", "bowtie-symbol-headphone"},
			{"icon_key", "bowtie-symbol-key"},
			{"icon_airplane", "bowtie-symbol-airplane"},
			{"icon_car", "bowtie-symbol-car"},
			{"icon_diamond", "bowtie-symbol-diamond"},
			{"icon_asterisk", "bowtie-symbol-asterisk"},
			{"icon_database_storage", "bowtie-symbol-storage-database"},
			{"icon_government", "bowtie-symbol-government"},
			{"icon_gavel", "bowtie-symbol-decision"},
			{"icon_parachute", "bowtie-symbol-parachute"},
			{"icon_paint_brush", "bowtie-symbol-paint-brush"},
			{"icon_palette", "bowtie-symbol-color-palette"},
			{"icon_gear", "bowtie-settings-gear"},
			{"icon_check_box", "bowtie-status-success-box"},
			{"icon_gift", "bowtie-package-fill"},
			{"icon_test_beaker", "bowtie-test-fill"},
			{"icon_broken_lightbulb", "bowtie-symbol-defect"},
			{"icon_clipboard_issue", "bowtie-symbol-issue"},
			{"icon_github", "bowtie-brand-github"},
			{"icon_pull_request", "bowtie-tfvc-pull-request"},
			{"icon_github_issue", "bowtie-status-error-outline"},
		};
	}

	public async Task<bool> ExportAsync()
	{
		ExportedWikiDoc? _wiki;
		var succeeded = true;
#pragma warning disable CA1031 // This is a general exception handler, we want to catch all exceptions
		try
		{
			var timer = Stopwatch.StartNew();

			if (_options.Path is null)
			{
				Log("Using current folder for export, -path is not set.");
				_wiki = ExportedWikiDoc.New(Directory.GetCurrentDirectory());
			}
			else
			{
				_wiki = ExportedWikiDoc.New(_options.Path);
			}

			if (_wiki is null)
			{
				Log("Wiki export path not found, please check the path.", LogLevel.Error);
				return false;
			}
			var files = GetFilesFromWiki(_wiki);

			Log($"Found {files.Count} total pages to process");

			var converter = new MarkdownConverter(_wiki, _options, _logger);
			var html = converter.ConvertToHtml(files);

			const string htmlStart = "<!DOCTYPE html><html>";
			const string htmlEnd = "</html>";
			const string headStart = "<head>";

			var footer = new List<string>();

			var header = new List<string>
				{
					"<meta http-equiv=Content-Type content=\"text/html; charset=utf-8\">"
				};
			const string headEnd = "</head>";

			html = await HandleMermaidAsync(html, header).ConfigureAwait(false);
			HandleMath(footer, header);
			HandleHighlightCode(header);
			html = await HandleDevOpsAsync(html).ConfigureAwait(false);
			await HandleCssAsync(footer).ConfigureAwait(false);

			// build the html for rendering
			html = $"{htmlStart}{headStart}{string.Concat(header)}{headEnd}<body>{html}<footer>{string.Concat(footer)}</footer></body>{htmlEnd}";

#if HTML_IN_MEMORY
			if (_options.Debug)
			{
				var htmlPath = string.Concat(_options.Output, ".html");
				Log($"Writing converted html to path: {htmlPath}");
				await File.WriteAllTextAsync(htmlPath, html).ConfigureAwait(false);
			}
			var generator = new PDFGenerator(_options, _logger);
			var path = await generator.ConvertHtmlToPDFAsync(html);
#else
			using var tempHtmlFile = new SelfDeletingTemporaryFile(html.Length, "html");
			await File.WriteAllTextAsync(tempHtmlFile.FilePath, html).ConfigureAwait(false);
			html = string.Empty;
			var generator = new PDFGenerator(_options, _logger);
			var path = await generator.ConvertHtmlToPDFAsync(tempHtmlFile).ConfigureAwait(false);
			if (_options.Debug)
			{
				var htmlPath = $"{_options.Output}.html";
				Log($"Writing converted html to path: {htmlPath}");
				tempHtmlFile.KeepAs(htmlPath);
				Log("HTML saved.");
			}
#endif
			_logger.LogMeasure($"Export done in {timer.Elapsed}");

			if (_options.Open)
			{
				using var fileopener = new Process();
				fileopener.StartInfo.FileName = "explorer";
				fileopener.StartInfo.Arguments = "\"" + Path.GetFullPath(path) + "\"";
				fileopener.Start();
			}
		}
		catch (Exception ex)
		{
			succeeded = false;
			Log($"Something bad happened.\n{ex}", LogLevel.Error);
		}
		finally
		{
#if !DEBUG
			Thread.Sleep(TimeSpan.FromSeconds(5));
#endif
		}
#pragma warning restore CA1031

		return succeeded;
	}

	private async Task HandleCssAsync(List<string> footer)
	{
		string cssPath;
		if (string.IsNullOrEmpty(_options.Css))
		{
			cssPath = Path.Join(AppDomain.CurrentDomain.BaseDirectory, "devopswikistyle.css");
			Log("No CSS specified, using devopswikistyle.css", LogLevel.Information, 0);
		}
		else
		{
			cssPath = Path.GetFullPath(_options.Css);
			if (!File.Exists(cssPath))
			{
				Log($"CSS file does not exist at path {cssPath}", LogLevel.Warning);
			}
		}

		if (!File.Exists(cssPath))
		{
			return;
		}

		var css = await File.ReadAllTextAsync(cssPath).ConfigureAwait(false);
		var style = $"<style>{css}</style>";

		// adding the css to the footer to overwrite the mermaid, katex, highlightjs styles.
		footer.Add(style);
	}

	private async Task<string> HandleDevOpsAsync(string html)
	{
		if (_options.AzureDevopsOrganization is not null)
		{
			var credentials = !string.IsNullOrEmpty(_options.AzureDevopsPAT)
				? new VssBasicCredential(string.Empty, _options.AzureDevopsPAT)
				: new VssClientCredentials();
			using var connection = new VssConnection(new Uri(_options.AzureDevopsOrganization), credentials);

			// Create instance of WorkItemTrackingHttpClient using VssConnection
			var witClient = await connection.GetClientAsync<WorkItemTrackingHttpClient>().ConfigureAwait(false);

			html = WorkItem().Replace(html, match => match.Groups[1].Value
				+ this.GenerateWorkItemLinkAsync(match.Groups[2].Value, witClient).Result
				+ match.Groups[3].Value);
		}

		return html;
	}

	private void HandleHighlightCode(List<string> header)
	{
		if (!_options.HighlightCode)
		{
			return;
		}
		// https://www.npmjs.com/package/highlight.js
		var highlightStyle = _options.HighlightStyle ?? "vs";
		var highlight = $"""
<link rel="stylesheet" href="https://cdnjs.cloudflare.com/ajax/libs/highlight.js/11.6.0/styles/{highlightStyle}.min.css">
<script src="https://cdnjs.cloudflare.com/ajax/libs/highlight.js/11.6.0/highlight.min.js"></script>
""";
		var highlightInitialize = "<script>hljs.highlightAll();</script>";

		// Avoid default highlight.js style to create a white background
		if (_options.HighlightStyle is null)
		{
			highlightInitialize += """
<style>
	.hljs {
		background: #f0f0f0;
	}
	pre {
		border-radius: 0px;
	}
</style>
""";
		}

		// todo: add offline version of highlightjs
		header.Add(highlight);
		header.Add(highlightInitialize);
	}

	private void HandleMath(List<string> footer, List<string> header)
	{
		if (!_options.Math)
		{
			return;
		}
		// https://www.npmjs.com/package/katex
		const string katex = "<script src=\"https://cdn.jsdelivr.net/npm/katex@0.16.2/dist/katex.min.js\"></script><script src=\"https://cdn.jsdelivr.net/npm/katex@0.13.11/dist/contrib/auto-render.min.js\" onload=\"renderMathInElement(document.body, {delimiters: [{left: '$$', right: '$$', display: true},{left: '$', right: '$', display: true}]});\"></script>";
		const string katexCss = "<link rel=\"stylesheet\" href=\"https://cdn.jsdelivr.net/npm/katex@0.16.2/dist/katex.min.css\">";

		header.Add(katexCss);
		footer.Add(katex);
	}

	private async Task<string> HandleMermaidAsync(string html, List<string> header)
	{
		if (_options.ConvertMermaid)
		{
			// https://github.com/mermaid-js/mermaid/releases
			var mermaid = !string.IsNullOrEmpty(_options.MermaidJsPath)
				? $"<script>{await File.ReadAllTextAsync(_options.MermaidJsPath).ConfigureAwait(false)}</script>"
				: @"<script src=""https://cdnjs.cloudflare.com/ajax/libs/mermaid/9.1.6/mermaid.min.js""></script>";

			const string mermaidInitialize = "<script>mermaid.initialize({ startOnLoad:true });</script>";

			// adding the correct charset for unicode smileys and all that fancy stuff, and include mermaid.js
			html = html + mermaid + mermaidInitialize;
			header.Add(mermaid);
			header.Add(mermaidInitialize);
		}
		return html;
	}

	private List<MarkdownFile> GetFilesFromWiki(ExportedWikiDoc _wiki)
	{
		if (!string.IsNullOrEmpty(_options.InputFile))
		{
			return new SingleFileScanner(_options.InputFile, _wiki.ExportPath(), _logger).Scan();
		}
		if (_options.IncludeUnlistedPages)
		{
			return new WikiDirectoryScanner(_wiki.ExportPath(), _options, _logger).Scan();
		}
		return new WikiOptionFilesScanner(_wiki.ExportPath(), _options, _logger).Scan();
	}

	private async Task<string> GenerateWorkItemLinkAsync(string stringId, WorkItemTrackingHttpClient witClient)
	{
		var stringIdWithoutHash = stringId.Replace("#", string.Empty, StringComparison.Ordinal);
		if (!int.TryParse(stringIdWithoutHash, CultureInfo.InvariantCulture, out var id))
		{
			Log($"Unable to parse work item id '{stringId}'", LogLevel.Warning);
			return stringId;
		}
		var workItem = await witClient.GetWorkItemAsync(id, expand: WorkItemExpand.All).ConfigureAwait(false);
		var type = await witClient.GetWorkItemTypeAsync(
			workItem.Fields["System.TeamProject"].ToString(),
			workItem.Fields["System.WorkItemType"].ToString())
			.ConfigureAwait(false);

		var childColor = type.Color;
		var childIcon = this._iconClass[type.Icon.Id];
		var url = ((ReferenceLink)workItem.Links.Links["html"]).Href;
		var title = workItem.Fields["System.Title"].ToString();
		var state = workItem.Fields["System.State"].ToString();
		var stateColor = type.States.First(s => s.Name == state).Color;

		return $"""
<span class="mention-widget-workitem" style="border-left-color: #{childColor};">
	<a class="mention-link mention-wi-link mention-click-handled" href="{url}">
		<span class="work-item-type-icon-host">
		<i class="work-item-type-icon bowtie-icon {childIcon}" role="figure" style="color: #{childColor};"></i>
		</span>
		<span class="secondary-text">{workItem.Id}</span>
		<span class="mention-widget-workitem-title fontWeightSemiBold">{title}</span>
	</a>
	<span class="mention-widget-workitem-state">
		<span class="workitem-state-color" style="color: #{stateColor};"></span>
		<span>{state}</span>
	</span>
</span>
""";
	}

	public void Log(string message, LogLevel logLevel = LogLevel.Information, int indent = 0)
	{
		_logger.Log(message, logLevel, indent);
	}

	[GeneratedRegex(@"([>\b \r\n])(#[0-9]+)([<\b \r\n])", RegexOptions.Compiled)]
	private static partial Regex WorkItem();
}

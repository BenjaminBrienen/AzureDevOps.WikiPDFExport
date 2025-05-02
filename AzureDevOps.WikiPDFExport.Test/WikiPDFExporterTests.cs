using System.IO;
using System.Threading.Tasks;
using NSubstitute;
using Xunit;

namespace AzureDevOps.WikiPDFExport.Test;

[Trait("Category", "Integration")]
public class WikiPDFExporterTests
{
	private readonly string _basePath = Path.Join("..", "..", "..", "test-data");
	private readonly ILoggerExtended _dummyLogger = Substitute.For<ILoggerExtended>();

	[Theory]
	[InlineData("Code")]
	[InlineData("DeepLink")]
	[InlineData("Dis-ordered")]
	[InlineData("Emoticons")]
	[InlineData("EmptyOrderFile")]
	[InlineData("Flat")]
	[InlineData("PngSvgExport")]
	[InlineData("SingleFileNoOrder")]
	[InlineData("WellFormed")]
	public async Task ExportWikiIncludeUnlistedPagesSucceeds(string wikiToExport)
	{
		var options = new Options
		{
			Path = Path.Join(_basePath, "Inputs", wikiToExport),
			Css = Path.Join(_basePath, "Inputs", "void.css"),
			Debug = true, // generates HTML
			IncludeUnlistedPages = true,
			Output = Path.Join(_basePath, "Outputs", $"{wikiToExport}.pdf"),
		};
		var export = new WikiPDFExporter(options, _dummyLogger);

		var ok = await export.ExportAsync();

		Assert.True(ok);
		var expectedHtmlPath = Path.Join(_basePath, "Expected", "IncludeUnlistedPages", $"{wikiToExport}.pdf.html");
		var outputHtmlPath = options.Output + ".html";
		Assert.True(File.Exists(outputHtmlPath));
		var expected = await File.ReadAllTextAsync(expectedHtmlPath);
		var actual = await File.ReadAllTextAsync(outputHtmlPath);
		Assert.Equal(expected, actual);
	}

	[Theory]
	[InlineData("Code")]
	[InlineData("DeepLink")]
	[InlineData("Dis-ordered")]
	[InlineData("Emoticons")]
	[InlineData("EmptyOrderFile")]
	[InlineData("Flat")]
	[InlineData("PngSvgExport")]
	[InlineData("SingleFileNoOrder")]
	[InlineData("WellFormed")]
	public async Task ExportWikiOnlyOrderListedPagesSucceeds(string wikiToExport)
	{
		var options = new Options
		{
			Path = Path.Join(_basePath, "Inputs", wikiToExport),
			Css = Path.Join(_basePath, "Inputs", "void.css"),
			Debug = true, // generates HTML
			IncludeUnlistedPages = false,
			Output = Path.Join(_basePath, "Outputs", $"{wikiToExport}.pdf"),
		};
		var export = new WikiPDFExporter(options, _dummyLogger);

		var ok = await export.ExportAsync();

		Assert.True(ok);
		var expectedHtmlPath = Path.Join(_basePath, "Expected", "OrderListedPages", $"{wikiToExport}.pdf.html");
		var outputHtmlPath = options.Output + ".html";
		Assert.True(File.Exists(outputHtmlPath));
		var expected = await File.ReadAllTextAsync(expectedHtmlPath);
		var actual = await File.ReadAllTextAsync(outputHtmlPath);
		Assert.Equal(expected, actual);
	}
}

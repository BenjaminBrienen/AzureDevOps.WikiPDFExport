using System.IO;
using NSubstitute;
using Xunit;

namespace AzureDevOps.WikiPDFExport.Test;

public class WikiOptionFilesScannerTests
{
	private static readonly string _basePath = Path.Join("..", "..", "..", "test-data");
	private readonly ILogger _dummyLogger = Substitute.For<ILogger>();

	[Fact]
	public void GivenWikiOptionFilesScannerWhenWikiHasPagesOutsideOrderFileThenOnlyThoseInOrderAreIncluded()
	{
		var options = new Options
		{
			Path = Path.Join(_basePath, "Inputs", "Dis-ordered"),
			Css = Path.Join(_basePath, "Inputs", "void.css"),
			Debug = true,
			IncludeUnlistedPages = false,
			Output = Path.Join(_basePath, "Outputs", "Dis-ordered"),
		};
		var scanner = new WikiOptionFilesScanner(options.Path, options, _dummyLogger);

		var files = scanner.Scan();

		Assert.Collection(files,
			first => Assert.Equal("/Mentioned-Section.md", first.FileRelativePath),
			second => Assert.Equal("/Mentioned-Section-No-Home.md", second.FileRelativePath));
	}

	[Fact]
	public void GivenWikiOptionFilesScannerWhenOnePatternIsExcludedThenTheFilesAreNotIncluded()
	{
		var options = new Options
		{
			Path = Path.Join(_basePath, "Inputs", "Dis-ordered"),
			Css = Path.Join(_basePath, "Inputs", "void.css"),
			Debug = true,
			IncludeUnlistedPages = false,
			Output = Path.Join(_basePath, "Outputs", "Exclude1"),
			ExcludePaths = ["Home"],
		};
		var scanner = new WikiOptionFilesScanner(options.Path, options, _dummyLogger);

		var files = scanner.Scan();

		var only = Assert.Single(files);
		Assert.Equal("/Mentioned-Section.md", only.FileRelativePath);
	}

	[Fact]
	public void GivenWikiOptionFilesScannerWhenTwoPatternAreExcludedThenTheFilesAreNotIncluded()
	{
		var options = new Options
		{
			Path = Path.Join(_basePath, "Inputs", "Code"),
			Css = Path.Join(_basePath, "Inputs", "void.css"),
			Debug = true,
			IncludeUnlistedPages = false,
			Output = Path.Join(_basePath, "Outputs", "Code"),
			ExcludePaths = ["Sub-Page2", "Customization"],
		};
		var scanner = new WikiOptionFilesScanner(options.Path, options, _dummyLogger);

		var files = scanner.Scan();

		Assert.Collection(files,
			first => Assert.Equal("/Another-Page.md", first.FileRelativePath),
			second => Assert.Equal("/Another-Page/Sub-Page1.md", second.FileRelativePath));
	}

	[Fact]
	public void GivenWikiOptionFilesScannerWhenWikiIsCodeExampleThenNoOrderChangeFromPreviousVersion()
	{
		var options = new Options
		{
			Path = Path.Join(_basePath, "Inputs", "Code"),
			Css = Path.Join(_basePath, "Inputs", "void.css"),
			Debug = true,
			IncludeUnlistedPages = false,
			Output = Path.Join(_basePath, "Outputs", "Code"),
		};
		var scanner = new WikiOptionFilesScanner(options.Path, options, _dummyLogger);

		var files = scanner.Scan();

		Assert.Collection(files,
			first => Assert.Equal("/Another-Page.md", first.FileRelativePath),
			second => Assert.Equal("/Another-Page/Sub-Page1.md", second.FileRelativePath),
			third => Assert.Equal("/Another-Page/Sub-Page2.md", third.FileRelativePath),
			fourth => Assert.Equal("/Another-Page/Sub-Page2/Sub-Page2a.md", fourth.FileRelativePath),
			fifth => Assert.Equal("/Admin-Layout-and-Customization.md", fifth.FileRelativePath));
	}
}

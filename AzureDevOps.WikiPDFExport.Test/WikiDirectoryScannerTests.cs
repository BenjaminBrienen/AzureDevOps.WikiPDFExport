using System.IO;
using NSubstitute;
using Xunit;

namespace AzureDevOps.WikiPDFExport.Test;

public class WikiDirectoryScannerTests
{
	private static readonly string _basePath = Path.Join("..", "..", "..", "test-data");
	private readonly ILogger _dummyLogger = Substitute.For<ILogger>();

	[Fact]
	public void GivenWikiDirectoryScannerWhenWikiHasPagesOutsideOrderFileThenTheyAreIncludedAsWell()
	{
		var options = new Options
		{
			Path = Path.Join(_basePath, "Inputs", "Dis-ordered"),
			Css = Path.Join(_basePath, "Inputs", "void.css"),
			Debug = true,
			IncludeUnlistedPages = true,
			Output = Path.Join(_basePath, "Outputs", "Dis-ordered"),
		};
		var scanner = new WikiDirectoryScanner(options.Path, options, _dummyLogger);

		var files = scanner.Scan();

		Assert.Collection(files,
			first => Assert.Equal("/Mentioned-Section.md", first.FileRelativePath),
			second => Assert.Equal("/Mentioned-Section/In-Mentioned-Section.md", second.FileRelativePath),
			third => Assert.Equal("/Start-Page.md", third.FileRelativePath),
			fourth => Assert.Equal("/Mentioned-Section-No-Home/In-Mentioned-Section-No-Home.md", fourth.FileRelativePath),
			fifth => Assert.Equal("/Unmentioned-Section/In-Unmentioned-Section.md", fifth.FileRelativePath));
	}

	[Fact]
	public void GivenWikiDirectoryScannerWhenOnePatternIsExcludedThenTheFilesAreNotIncluded()
	{
		var options = new Options
		{
			Path = Path.Join(_basePath, "Inputs", "Dis-ordered"),
			Css = Path.Join(_basePath, "Inputs", "void.css"),
			Debug = true,
			IncludeUnlistedPages = true,
			Output = Path.Join(_basePath, "Outputs", "Exclude1"),
			ExcludePaths = ["Home"],
		};
		var scanner = new WikiDirectoryScanner(options.Path, options, _dummyLogger);

		var files = scanner.Scan();

		Assert.Collection(files,
			first => Assert.Equal("/Mentioned-Section.md", first.FileRelativePath),
			second => Assert.Equal("/Mentioned-Section/In-Mentioned-Section.md", second.FileRelativePath),
			third => Assert.Equal("/Start-Page.md", third.FileRelativePath),
			fourth => Assert.Equal("/Unmentioned-Section/In-Unmentioned-Section.md", fourth.FileRelativePath));
	}

	[Fact]
	public void GivenWikiDirectoryScannerWhenTwoPatternAreExcludedThenTheFilesAreNotIncluded()
	{
		var options = new Options
		{
			Path = Path.Join(_basePath, "Inputs", "Dis-ordered"),
			Css = Path.Join(_basePath, "Inputs", "void.css"),
			Debug = true,
			IncludeUnlistedPages = true,
			Output = Path.Join(_basePath, "Outputs", "Exclude2"),
			ExcludePaths = ["In-.+Section", "Start"],
		};
		var scanner = new WikiDirectoryScanner(options.Path, options, _dummyLogger);

		var files = scanner.Scan();

		var f = Assert.Single(files);
		Assert.Equal("/Mentioned-Section.md", f.FileRelativePath);
	}

	[Fact]
	public void GivenWikiDirectoryScannerWhenWikiIsCodeExampleThenNoOrderChangeFromPreviousVersion()
	{
		var options = new Options
		{
			Path = Path.Join(_basePath, "Inputs", "Code"),
			Css = Path.Join(_basePath, "Inputs", "void.css"),
			Debug = true,
			IncludeUnlistedPages = true,
			Output = Path.Join(_basePath, "Outputs", "Code"),
		};
		var scanner = new WikiDirectoryScanner(options.Path, options, _dummyLogger);

		var files = scanner.Scan();

		Assert.Collection(files,
			first => Assert.Equal("/Another-Page.md", first.FileRelativePath),
			second => Assert.Equal("/Another-Page/Sub-Page1.md", second.FileRelativePath),
			third => Assert.Equal("/Another-Page/Sub-Page2.md", third.FileRelativePath),
			fourth => Assert.Equal("/Another-Page/Sub-Page2/Sub-Page2.md", fourth.FileRelativePath),
			fifth => Assert.Equal("/Admin-Layout-and-Customization.md", fifth.FileRelativePath));
	}
}

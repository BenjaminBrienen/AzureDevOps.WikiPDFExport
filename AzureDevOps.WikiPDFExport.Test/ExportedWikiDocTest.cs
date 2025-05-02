using System;
using System.IO;
using Xunit;

namespace AzureDevOps.WikiPDFExport.Test;

public class ExportedWikiDocTest
{
	private static readonly string _wikiPath = Path.Join("..", "..", "..", "test-data", "Inputs", "Code");
	private readonly DirectoryInfo codeWiki = new(Path.GetFullPath(_wikiPath, Environment.CurrentDirectory));

	[Fact]
	public void GivenValidWikiBaseLocationWhenCtorThenBaseIsFound()
	{
		var exportedWiki = ExportedWikiDoc.New(_wikiPath);
		Assert.NotNull(exportedWiki);
		Assert.NotNull(exportedWiki.BaseDirectory);
		Assert.Equal(codeWiki.FullName, exportedWiki.BaseDirectory.FullName);
	}

	[Fact]
	public void GivenValidWikiSubLocationWhenCtorThenBaseIsFound()
	{
		var exportedWiki = ExportedWikiDoc.New(Path.Join(_wikiPath, "Another-Page"));
		Assert.NotNull(exportedWiki);
		Assert.NotNull(exportedWiki.BaseDirectory);
		Assert.Equal(codeWiki.FullName, exportedWiki.BaseDirectory.FullName);
	}

	[Fact]
	public void GivenValidWikiSubSubLocationWhenCtorThenBaseIsFound()
	{
		var exportedWiki = ExportedWikiDoc.New(Path.Join(_wikiPath, "Another-Page", "Sub-Page2"));
		Assert.NotNull(exportedWiki);
		Assert.NotNull(exportedWiki.BaseDirectory);
		Assert.Equal(codeWiki.FullName, exportedWiki.BaseDirectory.FullName);
	}

	[Fact]
	public void GivenNoAttachmentsFolderWhenCtorThenBaseIsDefault()
	{
		var exportedWiki = ExportedWikiDoc.New("./");
		Assert.NotNull(exportedWiki);
		Assert.Equal("./", exportedWiki.BaseDirectory.ToString());
	}

	[Fact]
	public void GivenNonExistingLocationWhenCtorThenNull()
	{
		Assert.Null(ExportedWikiDoc.New(Path.Join("X:", "this", "hopefully", "does", "not", "exist")));
	}

	[Fact]
	public void GivenAbsoluteValidLocationWhenCtorThenSuccessful()
	{
		var exportedWiki = ExportedWikiDoc.New(Path.GetFullPath(_wikiPath, Environment.CurrentDirectory));
		Assert.NotNull(exportedWiki);
		Assert.NotNull(exportedWiki.BaseDirectory);
		Assert.NotEmpty(exportedWiki.BaseDirectory.FullName);
	}

	[Fact]
	public void GivenValidDirInfoWhenCtorThenSuccessful()
	{
		var exportedWiki = ExportedWikiDoc.New(codeWiki);
		Assert.NotNull(exportedWiki);
		Assert.NotNull(exportedWiki.BaseDirectory);
		Assert.NotEmpty(exportedWiki.BaseDirectory.FullName);
	}

	[Fact]
	public void GivenExportDocWhenGetPropertiesThenNoneAreBlank()
	{
		var exportedWiki = ExportedWikiDoc.New(codeWiki);
		Assert.NotNull(exportedWiki);
		Assert.NotNull(exportedWiki.BaseDirectory);
		AssertExistingValidDirectory(exportedWiki.ExportDirectory);
		AssertExistingValidDirectory(exportedWiki.BaseDirectory);
	}

	private static void AssertExistingValidDirectory(DirectoryInfo directory)
	{
		Assert.NotEmpty(directory.FullName);
		Assert.DoesNotMatch("^/s+$", directory.FullName);
		Assert.True(directory.Exists, $"{directory.Name} does not exist");
		Assert.True(directory.Extension.Length == 0 || directory.Extension == directory.Name,
			$"{directory.Name} has extension: {directory.Extension}");
	}
}

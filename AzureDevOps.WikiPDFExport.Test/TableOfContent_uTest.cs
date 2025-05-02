using Xunit;

namespace AzureDevOps.WikiPDFExport.Test;

public class TableOfContentUTest
{
	[Fact]
	public void CreateGlobalTableOfContentShouldReturnTocAndSingleHeaderLine()
	{
		// Arrange
		const string mdContent1 = """
		# SomeHeader
		SomeText
		""";

		// Act
		var result = MarkdownConverter.CreateGlobalTableOfContent([mdContent1]);

		// Assert
		Assert.Equal("[TOC]", result[0]);
		Assert.Equal("# SomeHeader", result[1]);
	}

	[Fact]
	public void CreateGlobalTableOfContentShouldNotReturnTocWhenNoHeaderFound()
	{
		// Arrange
		const string mdContent1 = """
		Only boring text
		No header here
		""";

		// Act
		var result = MarkdownConverter.CreateGlobalTableOfContent([mdContent1]);

		// Assert
		Assert.Empty(result);
	}

	[Fact]
	public void CreateGlobalTableOfContentShouldReturnTOCandMultipleHeaderLines()
	{
		// Arrange
		const string mdContent1 = """
		# SomeHeader
		SomeText
		""";
		const string mdContent2 = """
		## SomeOtherHeader
		[]() #Some very interesting text in wrong header format #
		""";

		// Act
		var result = MarkdownConverter.CreateGlobalTableOfContent([mdContent1, mdContent2]);

		// Assert
		Assert.Equal("[TOC]", result[0]);
		Assert.Equal("# SomeHeader", result[1]);
		Assert.Equal("## SomeOtherHeader", result[2]);
	}

	[Fact]
	public void CreateGlobalTableOfContentShouldIgnoreCodeSectionsSingle()
	{
		// Arrange
		const string mdContent1 = """
		``` code section
		# SomeHeader
		```
		""";

		// Act
		var result = MarkdownConverter.CreateGlobalTableOfContent([mdContent1]);

		// Assert
		Assert.Empty(result);
	}

	[Fact]
	public void CreateGlobalTableOfContentShouldIgnoreCodeSectionsSingleTilde()
	{
		// Arrange
		const string mdContent1 = """
		~~~ code section
		# SomeHeader
		~~~
		""";

		// Act
		var result = MarkdownConverter.CreateGlobalTableOfContent([mdContent1]);

		// Assert
		Assert.Empty(result);
	}

	[Fact]
	public void CreateGlobalTableOfContentShouldIgnoreCodeSectionsMultiple()
	{
		// Arrange
		const string mdContent1 = """
		``` code section
		## SomeHeader
		Some other text
		```
		```
		## Another header ```
		# A valid header
		""";

		// Act
		var result = MarkdownConverter.CreateGlobalTableOfContent([mdContent1]);

		// Assert
		Assert.Equal(2, result.Count);
		Assert.Equal("[TOC]", result[0]);
		Assert.Equal("# A valid header", result[1]);
	}

	[Fact]
	public void CreateGlobalTableOfContentShouldNotIgnoreInvalidCodeSections()
	{
		// Arrange
		const string mdContent1 = """
		Not at the beginning ```
		# A valid header
		```
		""";

		// Act
		var result = MarkdownConverter.CreateGlobalTableOfContent([mdContent1]);

		// Assert
		Assert.Equal(2, result.Count);
		Assert.Equal("[TOC]", result[0]);
		Assert.Equal("# A valid header", result[1]);
	}

	[Fact]
	public void CreateGlobalTableOfContentShouldNotIgnoreUnclosedCodeSections()
	{
		// Arrange
		const string mdContent1 = """
		``` unclosed comment
		# A valid header
		""";

		// Act
		var result = MarkdownConverter.CreateGlobalTableOfContent([mdContent1]);

		// Assert
		Assert.Equal(2, result.Count);
		Assert.Equal("[TOC]", result[0]);
		Assert.Equal("# A valid header", result[1]);
	}

	[Fact]
	public void RemoveDuplicatedHeadersFromGlobalToc()
	{
		// Arrange
		const string htmlContent = """
		<h1>SomeHeader</h1>
		<h2>SomeOtherHeader</h2>
		""";

		// Act
		var result = MarkdownConverter.RemoveDuplicatedHeadersFromGlobalToc(htmlContent);

		// Assert
		Assert.Equal(string.Empty, result);
	}

	[Fact]
	public void RemoveDuplicatedHeadersFromGlobalTocWhenIdsDefined()
	{
		// Arrange
		const string htmlContent = """
		<h1 id='interestingID'>SomeHeader</h1>
		<h2>SomeOtherHeader</h2>
		""";

		// Act
		var result = MarkdownConverter.RemoveDuplicatedHeadersFromGlobalToc(htmlContent);

		// Assert
		Assert.Equal(string.Empty, result);
	}

	[Fact]
	public void RemoveDuplicatedHeadersFromGlobalTOCExceptNavTag()
	{
		// Arrange
		const string nav = "<nav>Some cool nav content</nav>\n";
		const string htmlContent = nav
		+
		"""
		<h1>SomeHeader</h1>
		<h2>SomeOtherHeader</h2>
		""";

		// Act
		var result = MarkdownConverter.RemoveDuplicatedHeadersFromGlobalToc(htmlContent);

		// Assert
		Assert.Equal(nav.Trim('\n'), result);
	}
}

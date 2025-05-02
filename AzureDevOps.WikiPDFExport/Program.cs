using System.Threading.Tasks;
using AzureDevOps.WikiPDFExport;
using CommandLine;

var returnCode = await Parser.Default.ParseArguments<Options>(args)
	.MapResult(
		ExecuteWikiPDFExporter,
		_ => Task.FromResult(-1))
	.ConfigureAwait(false);

return returnCode;

static async Task<int> ExecuteWikiPDFExporter(Options options)
{
	var logger = new ConsoleLogger(options);
	var exporter = new WikiPDFExporter(options, logger);
	var succeeded = await exporter.ExportAsync().ConfigureAwait(false);
	return succeeded ? 0 : 1;
}

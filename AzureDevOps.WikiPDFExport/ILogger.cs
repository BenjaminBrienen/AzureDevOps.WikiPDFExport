using Microsoft.Extensions.Logging;

namespace AzureDevOps.WikiPDFExport;

#pragma warning disable CA1515 // These need to be public for mocking to work
public interface ILogger
{
	void Log(string message, LogLevel logLevel = LogLevel.Information, int indent = 0);
}

public interface ILoggerExtended : ILogger
{
	void LogMeasure(string message);
}
#pragma warning restore CA1515

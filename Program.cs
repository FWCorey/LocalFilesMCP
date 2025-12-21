using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var useStdio = args.Contains("--stdio");
var rootPath = GetRootPath(args);

// Set the static root path for file operations
FileOperationConfig.RootPath = rootPath;

if (useStdio)
{
    var builder = Host.CreateApplicationBuilder(args);
    // Configure all logs to go to stderr (stdout is used for the MCP protocol messages).
    builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);

    // MCP over stdio
    builder.Services
        .AddMcpServer()
        .WithStdioServerTransport()
        .WithTools<FileOperationTools>();

    // Start HTTP listener
    builder.Services.AddHostedService<HttpListenerService>();

    await builder.Build().RunAsync();
}
else
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Services
        .AddMcpServer()
        .WithHttpTransport()
        .WithTools<FileOperationTools>();

    var app = builder.Build();

    app.MapMcp();

    await app.RunAsync();
}

static string GetRootPath(string[] args)
{
    // Look for --root-path argument
    for (int i = 0; i < args.Length - 1; i++)
    {
        if (string.Equals(args[i], "--root-path", StringComparison.OrdinalIgnoreCase))
        {
            var path = args[i + 1];
            if (!string.IsNullOrWhiteSpace(path))
            {
                return Path.GetFullPath(path);
            }
        }
    }

    // Also support --root-path=value format
    foreach (var arg in args)
    {
        if (arg.StartsWith("--root-path=", StringComparison.OrdinalIgnoreCase))
        {
            var path = arg["--root-path=".Length..];
            if (!string.IsNullOrWhiteSpace(path))
            {
                return Path.GetFullPath(path);
            }
        }
    }

    // Default to current directory
    return Environment.CurrentDirectory;
}

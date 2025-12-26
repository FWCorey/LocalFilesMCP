using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var useStdio = args.Contains("--stdio");
var rootPath = GetRootPath(args);
ushort port = GetPortFromArgs(args);



// Set the static root path for file operations
MCPServerConfig.RootPath = rootPath;
MCPServerConfig.HttpPort = port;

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

    // Configure the server to listen on the specified port
    builder.WebHost.UseUrls($"http://localhost:{port}");

    builder.Services
        .AddMcpServer()
        .WithHttpTransport()
        .WithTools<FileOperationTools>();

    var app = builder.Build();

    app.MapMcp();

    await app.RunAsync();
}

static ushort GetPortFromArgs(string[] args) {

    for (int i = 0; i < args.Length - 1; i++)
    {
        if (string.Equals(args[i], "--port", StringComparison.OrdinalIgnoreCase))
        {
            if (ushort.TryParse(args[i + 1], out var port))
            {
                return port;
            }
        }
    }

    // Default port
    return 5000;
}

static string GetRootPath(string[] args)
{
    // Look for --root-path=value argument
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

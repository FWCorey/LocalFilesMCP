using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

Option<bool> stdioOption = new("--stdio");
Option<string> rootPathOption = new("--root-path")
{
    Arity = ArgumentArity.ZeroOrOne
};
Option<ushort> portOption = new("--port")
{
    DefaultValueFactory = _ => (ushort)5000
};

RootCommand rootCommand = new("LocalFilesMCP server");
rootCommand.Options.Add(stdioOption);
rootCommand.Options.Add(rootPathOption);
rootCommand.Options.Add(portOption);

rootCommand.SetAction(async (parseResult, cancellationToken) =>
{
    bool useStdio = parseResult.GetValue(stdioOption);
    string rootPath = parseResult.GetValue(rootPathOption) ?? Environment.CurrentDirectory;
    rootPath = Path.GetFullPath(rootPath);
    ushort port = parseResult.GetValue(portOption);

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

        await builder.Build().RunAsync(cancellationToken);
    } else {
        // 1. Use WebApplication instead of Host for HTTP support
        var builder = WebApplication.CreateBuilder(args);

        // 2. Configure Kestrel to listen on the specific port requested via args
        builder.WebHost.ConfigureKestrel(options => {
            options.ListenLocalhost(port);
        });

        // 3. Register MCP services
        builder.Services
            .AddMcpServer()
            .WithHttpTransport() // Registers SSE support
            .WithTools<FileOperationTools>();

        var app = builder.Build();

        // 4. IMPORTANT: Map the SSE endpoint so VS can connect to it
        // Depending on your specific MCP library version, this might be MapMcpServer, MapMcp, or similar.
        // The standard path is usually "/mcp" or "/sse".
        app.MapMcp("/mcp");

        await app.RunAsync(cancellationToken);
    }

    return 0;
});

return await rootCommand.Parse(args).InvokeAsync();

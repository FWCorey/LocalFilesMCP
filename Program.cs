using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

Option<bool> stdioOption = new("--stdio");
Option<string> volumeDescOption = new("--vol-desc")
{
    Arity = ArgumentArity.ZeroOrOne,
    Description = "Relative path to the volume description markdown file"
};
Option<string> rootPathOption = new("--root-path")
{
    Arity = ArgumentArity.ZeroOrOne,
    Description = "sandbox virtual volume path root"
};
Option<ushort> portOption = new("--port")
{
    Arity = ArgumentArity.ZeroOrOne,
    DefaultValueFactory = _ => (ushort)5000,
    Description = "the TCP port to use when hosting via http"
};

RootCommand rootCommand = new("LocalFilesMCP server");
rootCommand.Options.Add(stdioOption);
rootCommand.Options.Add(rootPathOption);
rootCommand.Options.Add(volumeDescOption);
rootCommand.Options.Add(portOption);

rootCommand.SetAction(async (parseResult, cancellationToken) =>
{
    bool useStdio = parseResult.GetValue(stdioOption);
    string rootPath = parseResult.GetValue(rootPathOption) ?? Environment.CurrentDirectory;
    rootPath = Path.GetFullPath(rootPath);
    ushort port = parseResult.GetValue(portOption);
    string descriptionPath = parseResult.GetValue(volumeDescOption) ?? string.Empty;
    
    MCPServerConfig.RootPath = rootPath;
    MCPServerConfig.HttpPort = port;
    MCPServerConfig.DescriptionPath = descriptionPath;

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

        // Start HTTP listener for debugging and extended compatibility
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

        // IMPORTANT: Here we map the SSE endpoint so VS can connect to it
        // Depending on your specific MCP library version, this might be MapMcpServer, MapMcp, or similar.
        // The standard path is usually "/mcp" or "/sse".
        app.MapMcp("/mcp");

        await app.RunAsync(cancellationToken);
    }

    return 0;
});

return await rootCommand.Parse(args).InvokeAsync();

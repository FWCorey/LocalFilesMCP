using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var useStdio = args.Contains("--stdio");

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

    // Register FileOperationTools for HTTP bridging (if you need to call methods directly)
    builder.Services.AddSingleton<FileOperationTools>();

    // Start HTTP listener
    builder.Services.AddHostedService<HttpListenerService>();

    await builder.Build().RunAsync();
}
else
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Services
        .AddMcpServer()
        .WithTools<FileOperationTools>();

    var app = builder.Build();

    app.MapMcp();

    await app.RunAsync();
}

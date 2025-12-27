using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

public sealed class HttpListenerService : BackgroundService
{
    private const string Prefix = "http://localhost:";
    private const string ToolRoute = "/mcp";

    private static readonly string[] AllowedMethods = { "GET", "POST", "OPTIONS" };

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly ILogger<HttpListenerService> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private HttpListener? _listener;
    
    public HttpListenerService(ILogger<HttpListenerService> logger, IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _listener = CreateListener();

        try
        {
            _listener.Start();
            _logger.LogInformation($"Local MCP bridge listening on {Prefix}{MCPServerConfig.HttpPort}/");

            while (!stoppingToken.IsCancellationRequested)
            {
                HttpListenerContext? context = null;

                try
                {
                    context = await _listener.GetContextAsync().WaitAsync(stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (HttpListenerException ex) when (ex.ErrorCode is 995 or 500)
                {
                    _logger.LogDebug(ex, "HttpListener stopped accepting connections.");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to accept HTTP request.");
                    continue;
                }

                if (context is null)
                {
                    continue;
                }

                _ = ProcessContextAsync(context, stoppingToken);
            }
        }
        finally
        {
            StopListener();
        }
    }

    private static HttpListener CreateListener()
    {
        var listener = new HttpListener
        {
            AuthenticationSchemes = AuthenticationSchemes.Anonymous,
            IgnoreWriteExceptions = true
        };

        listener.Prefixes.Add($"{Prefix}{MCPServerConfig.HttpPort}/");
        return listener;
    }

    private void StopListener()
    {
        if (_listener is null)
        {
            return;
        }

        try
        {
            if (_listener.IsListening)
            {
                _listener.Stop();
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to stop HttpListener cleanly.");
        }
        finally
        {
            _listener.Close();
            _listener = null;
        }
    }

    private async Task ProcessContextAsync(HttpListenerContext context, CancellationToken ct)
    {
        var request = context.Request;
        var response = context.Response;

        response.ContentEncoding = Encoding.UTF8;
        response.KeepAlive = false;
        ApplyCorsHeaders(response);

        try
        {
            if (!IsTargetRoute(request.Url))
            {
                await WriteJsonAsync(response, HttpStatusCode.NotFound, new { error = "Endpoint not found." }, ct);
                return;
            }

            if (HttpMethodEquals(request.HttpMethod, "OPTIONS"))
            {
                response.StatusCode = (int)HttpStatusCode.NoContent;
                return;
            }

            if (HttpMethodEquals(request.HttpMethod, "GET"))
            {
                if (IsSseRequest(request))
                {
                    await HandleSseAsync(context, ct);
                }
                else
                {
                    await WriteJsonAsync(response, HttpStatusCode.OK, new
                    {
                        status = "ok",
                        message = "Local MCP bridge is up",
                        methods = AllowedMethods,
                        supportsSse = false
                    }, ct);
                }

                return;
            }

            if (!HttpMethodEquals(request.HttpMethod, "POST"))
            {
                response.Headers["Allow"] = string.Join(", ", AllowedMethods);
                await WriteJsonAsync(response, HttpStatusCode.MethodNotAllowed, new { error = "Only POST is supported." }, ct);
                return;
            }

            var invocation = await DeserializeInvocationAsync(request, ct);
            if (invocation is null || string.IsNullOrWhiteSpace(invocation.Tool))
            {
                await WriteJsonAsync(response, HttpStatusCode.BadRequest, new { error = "Missing 'tool'." }, ct);
                return;
            }

            using var scope = _scopeFactory.CreateScope();
            var tools = scope.ServiceProvider.GetRequiredService<FileOperationTools>();

            var (statusCode, payload) = ExecuteTool(tools, invocation);
            await WriteJsonAsync(response, statusCode, payload, ct);
        }
        catch (JsonException jsonEx)
        {
            _logger.LogWarning(jsonEx, "Invalid JSON payload received.");
            await WriteJsonAsync(response, HttpStatusCode.BadRequest, new { error = "Invalid JSON payload." }, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled failure while processing request.");
            await WriteJsonAsync(response, HttpStatusCode.InternalServerError, new { error = ex.Message }, ct);
        }
        finally
        {
            TryCloseResponse(response);
        }
    }

    private static bool IsTargetRoute(Uri? url)
    {
        if (url is null)
        {
            return false;
        }

        var path = url.AbsolutePath.TrimEnd('/');
        return path.Equals(ToolRoute, StringComparison.OrdinalIgnoreCase);
    }

    private static bool HttpMethodEquals(string? method, string expected) =>
        string.Equals(method, expected, StringComparison.OrdinalIgnoreCase);

    private static void ApplyCorsHeaders(HttpListenerResponse response)
    {
        response.Headers["Access-Control-Allow-Origin"] = "*";
        response.Headers["Access-Control-Allow-Headers"] = "content-type";
        response.Headers["Access-Control-Allow-Methods"] = string.Join(", ", AllowedMethods);
    }

    private static async Task<ToolInvocation?> DeserializeInvocationAsync(HttpListenerRequest request, CancellationToken ct)
    {
        if (!request.HasEntityBody)
        {
            return null;
        }

        using var reader = new StreamReader(
            request.InputStream,
            request.ContentEncoding ?? Encoding.UTF8,
            detectEncodingFromByteOrderMarks: true,
            leaveOpen: false);

        var json = await reader.ReadToEndAsync(ct);
        return JsonSerializer.Deserialize<ToolInvocation>(json, SerializerOptions);
    }

    private static (HttpStatusCode StatusCode, object Payload) ExecuteTool(FileOperationTools tools, ToolInvocation invocation)
    {
        var args = invocation.Args;
        var toolName = invocation.Tool!.Trim();

        return toolName switch
        {
            "ListFiles" => (HttpStatusCode.OK, new { result = tools.ListFiles(GetArg<string>(args, "dir")) }),
            "ListFolders" => (HttpStatusCode.OK, new { result = tools.ListFolders(GetArg<string>(args, "dir")) }),
            "ReadFileText" => ExecuteReadFile(tools, args),
            _ => (HttpStatusCode.BadRequest, new { error = $"Unknown tool '{toolName}'." })
        };
    }

    private static (HttpStatusCode StatusCode, object Payload) ExecuteReadFile(FileOperationTools tools, Dictionary<string, JsonElement>? args)
    {
        var path = GetArg<string>(args, "path");
        if (string.IsNullOrWhiteSpace(path))
        {
            return (HttpStatusCode.BadRequest, new { error = "Missing 'path'." });
        }

        var content = tools.ReadFileText(path);
        return (HttpStatusCode.OK, new { result = content });
    }

    private static T? GetArg<T>(Dictionary<string, JsonElement>? args, string name)
    {
        if (args is null || !args.TryGetValue(name, out var element))
        {
            return default;
        }

        try
        {
            return element.Deserialize<T>(SerializerOptions);
        }
        catch
        {
            return default;
        }
    }

    private static async Task WriteJsonAsync(
        HttpListenerResponse response,
        HttpStatusCode statusCode,
        object payload,
        CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(payload, SerializerOptions);
        var bytes = Encoding.UTF8.GetBytes(json);

        response.StatusCode = (int)statusCode;
        response.ContentType = "application/json; charset=utf-8";
        response.ContentLength64 = bytes.Length;

        await response.OutputStream.WriteAsync(bytes, 0, bytes.Length, ct);
    }

    private static void TryCloseResponse(HttpListenerResponse response)
    {
        try
        {
            response.OutputStream.Close();
        }
        catch
        {
            // Ignore cleanup failures.
        }
    }

    private static bool IsSseRequest(HttpListenerRequest request)
    {   
        var accept = request.Headers["Accept"];
        if (!string.IsNullOrWhiteSpace(accept) && accept.Contains("text/event-stream", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var transport = request.QueryString?["transport"];
        return !string.IsNullOrEmpty(transport) && string.Equals(transport, "sse", StringComparison.OrdinalIgnoreCase);
    }

    private async Task HandleSseAsync(HttpListenerContext context, CancellationToken ct)
    {
        var response = context.Response;

        response.StatusCode = (int)HttpStatusCode.OK;
        response.ContentType = "text/event-stream";
        response.Headers["Cache-Control"] = "no-store";
        response.Headers["Connection"] = "keep-alive";
        response.Headers["X-Accel-Buffering"] = "no";
        response.SendChunked = true;
        response.KeepAlive = true;

        using var writer = new StreamWriter(response.OutputStream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false))
        {
            AutoFlush = true
        };

        await WriteSseEventAsync(writer, "ready", new
        {
            status = "ok",
            message = "Local MCP bridge streaming",
            tools = AllowedMethods
        });

        try
        {
            while (!ct.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(15), ct);
                await writer.WriteLineAsync(": keep-alive");
                await writer.WriteLineAsync();
            }
        }
        catch (OperationCanceledException)
        {
            // Graceful shutdown when the host stops or client disconnects.
        }
    }

    private static Task WriteSseEventAsync(StreamWriter writer, string? eventName, object payload)
    {
        var builder = new StringBuilder();

        if (!string.IsNullOrWhiteSpace(eventName))
        {
            builder.Append("event: ").Append(eventName).Append('\n');
        }

        var data = JsonSerializer.Serialize(payload, SerializerOptions);
        builder.Append("data: ").Append(data).Append("\n\n");

        return writer.WriteAsync(builder.ToString());
    }

    private sealed class ToolInvocation
    {
        public string? Tool { get; set; }
        public Dictionary<string, JsonElement>? Args { get; set; }
    }
}
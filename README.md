# LocalFilesMCP

LocalFilesMCP is an MCP server for file system operations, built with C# and the ModelContextProtocol SDK. It allows you to interact with the file system in a secure, sandboxed manner via the MCP protocol.

## Command-Line Arguments

- `--stdio`  
  Runs the server using standard input/output (stdio) transport. This is required for integration with tools like LM Studio or VS Code.

- `--root-path <path>`  
  Sets the root directory for all file operations. All file and directory access will be confined to this path.  
  Example:  
  ```
  dotnet run -- --stdio --root-path C:\My\Sandbox
  ```

- `--port`  
  Listener port for the server.  
  ```
  dotnet run -- --stdio --port 5000 --root-path=/home/user/sandbox
  ```

If `--root-path` is not specified, the current working directory is used as the root.
If `--port` is not specified, the server will listen on port 5000.
If `--stdio` is not specified, the server will use http.


## Adding LocalFilesMCP to LM Studio

1. **Build the project**  
   Run `dotnet publish -c Release -r win-x64` to produce a standalone executable.

2. **Locate the executable**  
   The output will be in `bin/Release/net10.0/<your-platform>/publish/`.

3. **Configure LM Studio**  
   Create or edit the `mcp.json` file in your LM Studio workspace directory.

4. **Add the server configuration**  
   Use the following example, updating the `command` path to your published executable and the `--root-path` as needed.

### Example `mcp.json` for LM Studio

```json
{
  "servers": {
    "LocalFilesMCP": {
      "type": "stdio",
      "command": "/absolute/path/to/LocalFilesMCP", // Update this path
      "args": [
        "--stdio",
        "--root-path=/absolute/path/to/your/sandbox"
      ]
    }
  }
}
```

- Replace `/absolute/path/to/LocalFilesMCP` with the full path to your published executable.
- Replace `/absolute/path/to/your/sandbox` with the directory you want to use as the root for file operations.

## More Information

- [ModelContextProtocol Documentation](https://modelcontextprotocol.io/)
- [Use MCP servers in LM Studio](https://lmstudio.ai/docs/app/mcp)
- [Use MCP servers in VS Code](https://code.visualstudio.com/docs/copilot/customization/mcp-servers)

---

This README provides setup, usage, and integration instructions for your solution.

# LocalFilesMCP

LocalFilesMCP is an MCP server for file system operations, built with C# and the ModelContextProtocol SDK.
It allows you to interact with the file system in a secure, sandboxed manner via the MCP protocol.

## Command-Line Arguments

- `--stdio`
  Runs the server using standard input/output (stdio) transport. This is required for integration with
  tools like LM Studio or VS Code.

- `--root-path <path>`
  Sets the root directory for all file operations. All file and directory access will be confined to
  this path.
  Example:
  ```bash
  dotnet run -- --stdio --root-path "C:\My\Sandbox"

  ```

* `--port`
Listener port for the server (only used if `--stdio` is NOT specified).
```bash
dotnet run -- --port 5000 --root-path "/home/user/sandbox"

```


If `--root-path` is not specified, the current working directory is used as the root.
If `--port` is not specified, the server will listen on port 5000.
If `--stdio` is not specified, the server will use HTTP/SSE transport.

## Adding LocalFilesMCP to LM Studio

1. **Build the project**
Run `dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true` to produce a standalone executable.
2. **Locate the executable**
The output will be in `bin/Release/net10.0/<your-platform>/publish/`.
3. **Configure LM Studio**
Create or edit the `mcp.json` file in your LM Studio workspace directory.
4. **Add the server configuration**
Use the following example, updating the `command` path to your published executable and the `--root-path` as needed.

### Example `mcp.json` for LM Studio

```json
{
  "mcpServers": {
    "LocalFilesMCP": {
      "command": "C:\\Path\\To\\LocalFilesMCP.exe",
      "args": [
        "--stdio",
        "--root-path=C:\\Path\\To\\Your\\Sandbox"
      ]
    }
  }
}

```

### Example `mcp.json` for CoPilot inVisual Studio

```json
{
  "inputs": [],
  "servers": {
    "LocalFilesMCP": {
      "command": "C:\\Path\\To\\LocalFilesMCP.exe",
      "args": [
        "--stdio",
        "--root-path=C:\\Path\\To\\Your\\Sandbox"
      ],
      "env": {}
    },
    "OtherLocalFilesMCP": {
      "command": "C:\\Path\\To\\LocalFilesMCP.exe",
      "args": [
        "--stdio",
        "--root-path=C:\\Path\\To\\Your\\Other\\Sandbox"
      ],
      "env": {}
    }
  }
}

```

## Tools

The following tools are available in the LocalFilesMCP project:

- **ListFolders**: Lists folders in the specified directory.
  - **Description**: Lists all folders in the given directory. Defaults to the current directory if no directory is specified.
  - **Parameters**:
    - `directory` (optional): The directory to list.

- **ListFiles**: Lists files in the specified directory.
  - **Description**: Lists all files in the given directory. Defaults to the current directory if no directory is specified.
  - **Parameters**:
    - `directory` (optional): The directory to list.

- **ReadFileText**: Reads the text contents of a file.
  - **Description**: Reads and returns the text content of the specified file.
  - **Parameters**:
    - `path`: The path to the file to read.

- **ReadBinaryFile**: Reads the binary contents of a file.
  - **Description**: Reads and returns the binary content of the specified file.
  - **Parameters**:
    - `path`: The path to the file to read.

- **ChangeDir**: Changes the current working directory within the root.
  - **Description**: Changes the current working directory to the specified path and returns the new absolute path or an error message.
  - **Parameters**:
    - `path`: The target directory path, relative to the root. Absolute paths or drive letters are not allowed.

## More Information

* [ModelContextProtocol Documentation](https://modelcontextprotocol.io/)
* [Use MCP servers in LM Studio](https://lmstudio.ai/docs/app/mcp)
* [Use MCP servers in VS Code](https://code.visualstudio.com/docs/copilot/customization/mcp-servers)

---

## License

This project is licensed under the **Apache License 2.0 with the Commons Clause**.

**What this means:**

* **Free for Personal Use:** You can download, modify, and use this freely.
* **Free for Internal Business Use:** You can use this inside your company (e.g., game studios, enterprise workflows).
* **No Commercial Resale:** You cannot sell this software, offer it as a paid service, or wrap it in a commercial product for third parties.

See [LICENSE.txt](https://www.google.com/search?q=LICENSE.txt) for the full legal text.

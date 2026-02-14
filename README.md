# LocalFilesMCP

LocalFilesMCP is an MCP server for file system operations, built with C# and the ModelContextProtocol SDK.
It allows an Agent to interact with the file system in a secure, sandboxed manner via the MCP protocol.

## What's New in 0.4.2-beta (Breaking)

- **Added** `Find` and `FReg` tools for glob-based file search and regex content search.
- **Removed** `ChangeDir` and `ListFiles`. All tools are now stateless — use the `directory` parameter on `Find`, `FReg`, and `ListFolders` instead. **(Breaking)**
- **Fixed** `GetVolumeDescription` now reads the file specified by `--vol-desc` (was unimplemented).
- **Fixed** `ListFolders` and `FReg` now return paths relative to the volume root.

See the full [CHANGELIST](https://github.com/FWCorey/LocalFilesMCP/blob/main/CHANGELIST.md) for details.

---

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

---

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

---

## Volume Description

The Volume Description file is used to inform agents about the contents and purpose of the filesystem volume exposed
via MCP. This helps agents like CoPilot and Cursor understand how to interact with the files and directories within
the volume and not override the solution files context. The description should be clear and concise, providing essential
information without overwhelming detail.

Must be in markdown format.
Must start with a **# Volume Description** header.
You may include any user comments in this header, they will not be returned to the agent.
You may include additional sections as needed for your specific LLM agent.

Recommended to use a filename that starts with a "." to infer it is hidden.

Recommended to include the following sections:
- **## Purpose**: A brief explanation of what the volume contains and how agents should interpret it.
- **## Notes**: Any important details or caveats about the volume, such as:
  - Paths are relative to the volume root.
  - This volume may or may not contain project files.
  - This volume is read-only.

### Example:
```markdown
# Volume Description

This is a read-only filesystem volume exposed via MCP.

## Purpose
This volume contains files and directories that the agent can read and analyze to assist with queries about FooBar projects.

## Notes
- Paths are relative to the volume root.
- This volume may contain code files for example only but will not contain any project files.
- This volume is read-only.
```
---

## Tools

The following tools are available in the LocalFilesMCP project:

- **GetVolumeDescription**: Retrieves the volume description.
  - **Description**: Returns a markdown string describing the contents and purpose of the filesystem volume.
  - **Parameters**: None.

- **ListFolders**: Lists folders in the specified directory.
  - **Description**: Lists all folders in the given directory. Defaults to the current directory if no directory is specified.
  - **Parameters**:
    - `directory` (optional): The directory to list.

- **Find**: Searches for files matching a glob pattern.
  - **Description**: Searches for files matching a glob pattern (e.g., `**/*.cs`, `src/**/*.txt`) within the volume.
  - **Parameters**:
    - `pattern`: Glob pattern to match files.
    - `directory` (optional): Directory to search from. Defaults to the current directory.

- **FReg**: Searches file contents for a regex pattern.
  - **Description**: Searches file contents by regex pattern with configurable output modes and optional context lines.
  - **Parameters**:
    - `pattern`: Regex pattern to search for in file contents.
    - `directory` (optional): Directory to search in. Defaults to the current directory.
    - `fileGlob` (optional): Glob pattern to filter which files are searched (e.g., `*.cs`). Defaults to all files.
    - `outputMode` (optional): `files` returns only matching file paths, `content` returns file paths with matching lines and line numbers. Defaults to `content`.
    - `contextLines` (optional): Number of context lines to include before and after each match. Defaults to 0.

- **ReadFileText**: Reads the text contents of a file.
  - **Description**: Reads and returns the text content of the specified file.
  - **Parameters**:
    - `path`: The path to the file to read.

- **ReadBinaryFile**: Reads the binary contents of a file.
  - **Description**: Reads and returns the binary content of the specified file.
  - **Parameters**:
    - `path`: The path to the file to read.

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

# Changelog

## [0.4.2-beta]

### Fixed
- **ListFolders** tool: Now returns paths relative to the volume root instead of absolute paths.
- **FReg** tool: Now returns file paths relative to the volume root instead of relative to the search directory.

## [0.4.1-beta]

### Fixed
- **GetVolumeDescription** tool: Implemented file reading (was previously `NotImplementedException`). Returns the contents of the markdown file specified by `--vol-desc`. Returns an error message if no description path is configured or the file is missing.

## [0.4.0-beta]

### Added
- **Find** tool: Search for files matching glob patterns (e.g., `**/*.cs`) within the volume. Uses `Microsoft.Extensions.FileSystemGlobbing` for full glob support.
- **FReg** tool: Search file contents by regex pattern with configurable output modes (`files` or `content`) and optional context lines.
- Added `Microsoft.Extensions.FileSystemGlobbing` NuGet dependency.
- Volume description support via `--vol-desc` CLI argument and `DescriptionPath` config field.
- Tools reference the volume context in descriptions and error messages.
- Tools section added to README.

### Removed (Breaking)
- **ChangeDir** tool: Removed stateful current directory tracking. All tools now accept an explicit `directory` parameter, making every call stateless and thread-safe.
- **ListFiles** tool: Superseded by `Find` (e.g., `Find("*", directory)` for single-directory listing, `Find("**/*")` for recursive).

> **Breaking change:** Clients relying on `ChangeDir` or `ListFiles` must migrate to `Find` with an explicit `directory` parameter.

## [0.2.0-beta]

### Added
- Configurable `--port` CLI argument for HTTP/SSE transport.
- NuGet packaging support (`PackAsTool`, package icon, license, README).
- Publish profile for self-contained single-file executable.
- `MCPServerConfig` static configuration class (replaced `FileOperationConfig`).
- Apache License 2.0 with Commons Clause.

### Changed
- Restructured CLI argument parsing with `System.CommandLine`.
- Improved HTTP listener service with SSE keep-alive support.

## [0.1.0-beta]

### Added
- Initial release.
- MCP server with stdio and HTTP/SSE transport modes.
- **ListFolders**, **ListFiles**, **ReadFileText**, **ReadBinaryFile**, **ChangeDir** tools.
- Path sandboxing via `TryGetSafePath()` — all operations confined under root path.
- `HttpListenerService` background service for debugging alongside stdio mode.

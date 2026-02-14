# Changelog

## [Unreleased]

### Added
- **Find** tool: Search for files matching glob patterns (e.g., `**/*.cs`) within the volume. Uses `Microsoft.Extensions.FileSystemGlobbing` for full glob support.
- **FReg** tool: Search file contents by regex pattern with configurable output modes (`files` or `content`) and optional context lines.
- Added `Microsoft.Extensions.FileSystemGlobbing` NuGet dependency.

### Removed (Breaking)
- **ChangeDir** tool: Removed stateful current directory tracking. All tools now accept an explicit `directory` parameter, making every call stateless and thread-safe.
- **ListFiles** tool: Superseded by `Find` (e.g., `Find("*", directory)` for single-directory listing, `Find("**/*")` for recursive).

> **Breaking change:** Clients relying on `ChangeDir` or `ListFiles` must migrate to `Find` with an explicit `directory` parameter.

## [0.4.1-beta]

### Fixed
- **GetVolumeDescription** tool: Implemented file reading (was previously `NotImplementedException`). Returns the contents of the markdown file specified by `--vol-desc`. Returns an error message if no description path is configured or the file is missing.
- **ListFolders** tool: Now returns paths relative to the volume root instead of absolute paths.
- **FReg** tool: Now returns file paths relative to the volume root instead of relative to the search directory.

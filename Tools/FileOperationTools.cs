using System.ComponentModel;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;
using ModelContextProtocol.Server;

/// <summary>
/// MCP tools for file system operations.
/// </summary>
public class FileOperationTools
{
    // Root of the accessible file system for this tool. All operations are confined under this path.
    private readonly string _rootPath;

    public FileOperationTools()
    {
        _rootPath = Path.GetFullPath(MCPServerConfig.RootPath);
    }

    /// <summary>
    /// Resolves and validates a user-provided path so it is confined under RootPath without throwing.
    /// Returns false with an error message when invalid. When true, safePath is an absolute path.
    /// </summary>
    internal bool TryGetSafePath(string? path, bool mustExist, bool expectDirectory, out string? safePath, out string? error)
    {
        safePath = null;
        error = null;

        if (string.IsNullOrWhiteSpace(path))
        {
            path = expectDirectory ? "." : null;
            if (path is null)
            {
                error = "Error: Path cannot be empty for file operations.";
                return false;
            }
        }

        bool isRooted;
        try
        {
            isRooted = Path.IsPathRooted(path);
        }
        catch
        {
            error = "Error: Invalid path format.";
            return false;
        }

        if (isRooted)
        {
            error = "Error: Absolute or UNC paths are not allowed. Provide a path relative to the current directory within the root.";
            return false;
        }

        string fullPath;
        try
        {
            var combined = Path.Combine(_rootPath, path);
            fullPath = Path.GetFullPath(combined);
        }
        catch (Exception ex)
        {
            error = $"Error: Failed to resolve path: {ex.Message}";
            return false;
        }

        string rootWithSep;
        try
        {
            var sep = Path.DirectorySeparatorChar.ToString();
            rootWithSep = _rootPath.EndsWith(sep, StringComparison.Ordinal) ? _rootPath : _rootPath + sep;
        }
        catch
        {
            rootWithSep = _rootPath + Path.DirectorySeparatorChar;
        }

        if (!fullPath.StartsWith(rootWithSep, StringComparison.OrdinalIgnoreCase) && !fullPath.Equals(_rootPath, StringComparison.OrdinalIgnoreCase))
        {
            error = "Error: The resolved path is outside of the allowed root.";
            return false;
        }

        if (mustExist)
        {
            try
            {
                if (expectDirectory)
                {
                    if (!Directory.Exists(fullPath))
                    {
                        error = $"Error: Directory not found: {fullPath}";
                        return false;
                    }
                }
                else
                {
                    if (!File.Exists(fullPath))
                    {
                        error = $"Error: File not found: {fullPath}";
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                error = $"Error: Unable to verify existence: {ex.Message}";
                return false;
            }
        }

        safePath = fullPath;
        return true;
    }

    [McpServerTool]
    [Description("Volume Description contains the context and purpose of the filesystem volume exposed via this MCP")]
    public string GetVolumeDescription()
    {
        if (string.IsNullOrWhiteSpace(MCPServerConfig.DescriptionPath))
        {
            return "Error: No volume description configured. Launch the server with --vol-desc <path> to specify one.";
        }

        if (!TryGetSafePath(MCPServerConfig.DescriptionPath, mustExist: true, expectDirectory: false, out var safePath, out var error))
        {
            return error ?? "Error: Invalid volume description path.";
        }

        try
        {
            return File.ReadAllText(safePath!);
        }
        catch (Exception ex)
        {
            return $"Error: Failed to read volume description: {ex.Message}";
        }
    }

    [McpServerTool]
    [Description("Lists folders in the specified directory of this volume.")]
    public string[] ListFolders(
        [Description("Directory to list. Defaults to the current directory when not provided.")] string? directory = null)
    {
        var dirArg = string.IsNullOrWhiteSpace(directory) ? "." : directory;
        if (!TryGetSafePath(dirArg, mustExist: true, expectDirectory: true, out var dir, out _))
        {
            return Array.Empty<string>();
        }

        try
        {
            return Directory.EnumerateDirectories(dir!)
                .Select(d => Path.GetRelativePath(_rootPath, d))
                .ToArray();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

[McpServerTool]
    [Description("Reads the text contents of a file in this volume.")]
    public string ReadFileText(
        [Description("Path to the file to read.")] string path)
    {
        if (!TryGetSafePath(path, mustExist: true, expectDirectory: false, out var safePath, out var error))
        {
            return error ?? "Error: Invalid path.";
        }

        try
        {
            return File.ReadAllText(safePath!);
        }
        catch (IOException ioEx)
        {
            return $"Error: I/O failure while reading file: {ioEx.Message} in this volume";
        }
        catch (UnauthorizedAccessException uaEx)
        {
            return $"Error: Access denied while reading file: {uaEx.Message} in this volume";
        }
        catch (Exception ex)
        {
            return $"Error: Unexpected failure while reading file: {ex.Message} in this volume";
        }
    }

    [McpServerTool]
    [Description("Reads the binary contents of a file in this volume.")]
    public byte[] ReadBinaryFile(
        [Description("Path to the file  in this volume to read.")] string path)
    {
        if (!TryGetSafePath(path, mustExist: true, expectDirectory: false, out var safePath, out _))
        {
            return Array.Empty<byte>();
        }

        try
        {
            return File.ReadAllBytes(safePath!);
        }
        catch
        {
            return Array.Empty<byte>();
        }
    }

    [McpServerTool]
    [Description("Searches for files matching a glob pattern within this volume.")]
    public string[] Find(
        [Description("Glob pattern to match files (e.g., **/*.cs, src/**/*.txt).")] string pattern,
        [Description("Directory to search from. Defaults to the current directory.")] string? directory = null)
    {
        var dirArg = string.IsNullOrWhiteSpace(directory) ? "." : directory;
        if (!TryGetSafePath(dirArg, mustExist: true, expectDirectory: true, out var dir, out _))
        {
            return Array.Empty<string>();
        }

        try
        {
            var matcher = new Matcher();
            matcher.AddInclude(pattern);

            var directoryInfo = new DirectoryInfoWrapper(new DirectoryInfo(dir!));
            var result = matcher.Execute(directoryInfo);

            return result.Files
                .Select(f => f.Path)
                .ToArray();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    [McpServerTool]
    [Description("Searches file contents for a regex pattern within this volume.")]
    public string FReg(
        [Description("Regex pattern to search for in file contents.")] string pattern,
        [Description("Directory to search in. Defaults to the current directory.")] string? directory = null,
        [Description("Glob pattern to filter which files are searched (e.g., *.cs). Defaults to all files.")] string? fileGlob = null,
        [Description("Output mode: 'files' returns only matching file paths, 'content' returns file paths with matching lines and line numbers. Defaults to 'content'.")] string? outputMode = null,
        [Description("Number of context lines to include before and after each match. Only applies when outputMode is 'content'. Defaults to 0.")] int contextLines = 0)
    {
        var dirArg = string.IsNullOrWhiteSpace(directory) ? "." : directory;
        if (!TryGetSafePath(dirArg, mustExist: true, expectDirectory: true, out var dir, out var error))
        {
            return error ?? "Error: Invalid directory.";
        }

        Regex regex;
        try
        {
            regex = new Regex(pattern, RegexOptions.Compiled, TimeSpan.FromSeconds(5));
        }
        catch (ArgumentException ex)
        {
            return $"Error: Invalid regex pattern: {ex.Message}";
        }

        // Enumerate files, optionally filtered by glob
        string[] files;
        try
        {
            if (!string.IsNullOrWhiteSpace(fileGlob))
            {
                var matcher = new Matcher();
                matcher.AddInclude(fileGlob);
                var directoryInfo = new DirectoryInfoWrapper(new DirectoryInfo(dir!));
                var globResult = matcher.Execute(directoryInfo);
                files = globResult.Files
                    .Select(f => Path.Combine(dir!, f.Path))
                    .ToArray();
            }
            else
            {
                files = Directory.EnumerateFiles(dir!, "*", SearchOption.AllDirectories).ToArray();
            }
        }
        catch (Exception ex)
        {
            return $"Error: Failed to enumerate files: {ex.Message}";
        }

        var filesMode = string.Equals(outputMode, "files", StringComparison.OrdinalIgnoreCase)
                     || string.Equals(outputMode, "files_with_matches", StringComparison.OrdinalIgnoreCase);

        var results = new List<string>();

        foreach (var file in files)
        {
            // Validate each file stays within root
            var relativePath = Path.GetRelativePath(dir!, file);
            if (!TryGetSafePath(relativePath, mustExist: true, expectDirectory: false, out _, out _))
                continue;

            string[] lines;
            try
            {
                lines = File.ReadAllLines(file);
            }
            catch
            {
                continue; // Skip unreadable files
            }

            var fileRelative = Path.GetRelativePath(_rootPath, file);
            var matchedLineIndices = new List<int>();

            for (int i = 0; i < lines.Length; i++)
            {
                try
                {
                    if (regex.IsMatch(lines[i]))
                    {
                        matchedLineIndices.Add(i);
                    }
                }
                catch (RegexMatchTimeoutException)
                {
                    break; // Skip file on timeout
                }
            }

            if (matchedLineIndices.Count == 0)
                continue;

            if (filesMode)
            {
                results.Add(fileRelative);
            }
            else
            {
                // Content mode: output matching lines with optional context
                var emittedLines = new HashSet<int>();
                foreach (var lineIdx in matchedLineIndices)
                {
                    int start = Math.Max(0, lineIdx - contextLines);
                    int end = Math.Min(lines.Length - 1, lineIdx + contextLines);

                    for (int i = start; i <= end; i++)
                    {
                        if (emittedLines.Add(i))
                        {
                            var prefix = i == lineIdx ? ":" : "-";
                            results.Add($"{fileRelative}{prefix}{i + 1}{prefix} {lines[i]}");
                        }
                    }
                }
            }
        }

        return results.Count == 0 ? "No matches found." : string.Join("\n", results);
    }

}
using System.ComponentModel;
using System.IO;
using ModelContextProtocol.Server;

/// <summary>
/// MCP tools for file system operations.
/// </summary>
internal class FileOperationTools
{
    // Root of the accessible file system for this tool. All operations are confined under this path.
    private static readonly string RootPath = Path.GetFullPath(Environment.CurrentDirectory);

    private static string CurrentPath { get; set; } = Environment.CurrentDirectory;

    private static string CurrentRelativePath
    {
        get
        {
            try
            {
                var relativePath = Path.GetRelativePath(RootPath, CurrentPath);
                return string.IsNullOrEmpty(relativePath) ? "." : relativePath;
            }
            catch
            {
                return ".";
            }
        }
    }

    /// <summary>
    /// Resolves and validates a user-provided path so it is confined under RootPath without throwing.
    /// Returns false with an error message when invalid. When true, safePath is an absolute path.
    /// </summary>
    private static bool TryGetSafePath(string? path, bool mustExist, bool expectDirectory, out string? safePath, out string? error)
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
            var combined = Path.Combine(CurrentPath, path);
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
            rootWithSep = RootPath.EndsWith(sep, StringComparison.Ordinal) ? RootPath : RootPath + sep;
        }
        catch
        {
            rootWithSep = RootPath + Path.DirectorySeparatorChar;
        }

        if (!fullPath.StartsWith(rootWithSep, StringComparison.OrdinalIgnoreCase))
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
    [Description("Lists folders in the specified directory.")]
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
                .ToArray();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    [McpServerTool]
    [Description("Lists files in the specified directory.")]
    public string[] ListFiles(
        [Description("Directory to list. Defaults to the current directory when not provided.")] string? directory = null)
    {
        var dirArg = string.IsNullOrWhiteSpace(directory) ? "." : directory;
        if (!TryGetSafePath(dirArg, mustExist: true, expectDirectory: true, out var dir, out _))
        {
            return Array.Empty<string>();
        }

        try
        {
            return Directory.EnumerateFiles(dir!)
                .ToArray();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    [McpServerTool]
    [Description("Reads the text contents of a file.")]
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
            return $"Error: I/O failure while reading file: {ioEx.Message}";
        }
        catch (UnauthorizedAccessException uaEx)
        {
            return $"Error: Access denied while reading file: {uaEx.Message}";
        }
        catch (Exception ex)
        {
            return $"Error: Unexpected failure while reading file: {ex.Message}";
        }
    }

    [McpServerTool]
    [Description("Reads the binary contents of a file.")]
    public byte[] ReadBinaryFile(
        [Description("Path to the file to read.")] string path)
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
    [Description("Changes the current working directory within the root and returns the new absolute path or an error message.")]
    public string ChangeDir(
        [Description("Target directory path, relative to the root. Absolute paths or drive letters are not allowed.")] string path)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                path = ".";
            }

            bool isRooted;
            try
            {
                isRooted = Path.IsPathRooted(path);
            }
            catch
            {
                return "Error: Invalid path format.";
            }

            if (isRooted)
            {
                return "Error: Absolute paths are not allowed. Provide a path relative to the root.";
            }

            string candidate;
            try
            {
                candidate = Path.GetFullPath(Path.Combine(RootPath, path));
            }
            catch (Exception ex)
            {
                return $"Error: Failed to resolve directory: {ex.Message}";
            }

            string rootWithSep;
            try
            {
                var sep = Path.DirectorySeparatorChar.ToString();
                rootWithSep = RootPath.EndsWith(sep, StringComparison.Ordinal) ? RootPath : RootPath + sep;
            }
            catch
            {
                rootWithSep = RootPath + Path.DirectorySeparatorChar;
            }

            if (!candidate.StartsWith(rootWithSep, StringComparison.OrdinalIgnoreCase))
            {
                return "Error: Target directory is outside of the allowed root.";
            }

            try
            {
                if (!Directory.Exists(candidate))
                {
                    return $"Error: Directory not found: {candidate}";
                }
            }
            catch (Exception ex)
            {
                return $"Error: Unable to verify directory: {ex.Message}";
            }

            CurrentPath = candidate;
            return CurrentRelativePath;
        }
        catch (IOException ioEx)
        {
            return $"Error: I/O failure while changing directory: {ioEx.Message}";
        }
        catch (UnauthorizedAccessException uaEx)
        {
            return $"Error: Access denied while changing directory: {uaEx.Message}";
        }
        catch (Exception ex)
        {
            return $"Error: Unexpected failure while changing directory: {ex.Message}";
        }
    }
}
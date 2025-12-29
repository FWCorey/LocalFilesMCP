public static class MCPServerConfig
{
    /// <summary>
    /// Contains the relative path to the volume description
    /// </summary>
    public static string DescriptionPath { get; set; } = string.Empty;

    /// <summary>
    /// Root path of the volume
    /// </summary>
    public static string RootPath { get; set; } = Environment.CurrentDirectory;

    /// <summary>
    /// TCP Port to use when server transport is http
    /// </summary>
    public static ushort HttpPort { get; set; } = 5000;
}

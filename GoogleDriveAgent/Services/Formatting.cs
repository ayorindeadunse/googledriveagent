namespace GoogleDriveAgent.Services;

public static class Formatting
{
    private static readonly string[] Units = { "B", "KB", "MB", "GB", "TB" };

    public static string FormatSize(long? bytes)
    {
        if (bytes is null)
        {
            return "-";
        }

        double size = bytes.Value;
        var unit = 0;
        while (size >= 1024 && unit < Units.Length - 1)
        {
            size /= 1024;
            unit++;
        }

        return $"{size:0.#} {Units[unit]}";
    }
}

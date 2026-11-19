using System.IO;
using System.Text.Json;

namespace Jime;

internal static class PositionSettings
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Jime",
        "window-position.json");

    public static WindowPosition? Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return null;
            var position = JsonSerializer.Deserialize<WindowPosition>(File.ReadAllText(FilePath));
            return position is { } && double.IsFinite(position.Left) && double.IsFinite(position.Top)
                ? position
                : null;
        }
        catch
        {
            return null;
        }
    }

    public static void Save(WindowPosition position)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        using var stream = File.Create(FilePath);
        JsonSerializer.Serialize(stream, position);
    }
}

internal sealed record WindowPosition(double Left, double Top);

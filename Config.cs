using System.IO;
using System.Text.Json;

namespace HyperOSFolder;

internal sealed class WidgetSettings
{
    public string FolderPath = "";
    public double? X;
    public double? Y;
    public double? Width;
    public double? Height;
    public int GridMode = 3;
    public bool HideExtensions = true;
    public Dictionary<string, double[]> Positions = new();
    public bool HasPosition => X.HasValue && Y.HasValue;
}

internal static class Config
{
    private static readonly string FilePath = Path.Combine(AppContext.BaseDirectory, "config.json");

    public static List<WidgetSettings> Widgets = new();

    public static WidgetSettings Primary
    {
        get
        {
            if (Widgets.Count == 0)
                Widgets.Add(new WidgetSettings
                {
                    FolderPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)
                });
            return Widgets[0];
        }
    }

    public static void Load()
    {
        Widgets.Clear();
        try
        {
            if (File.Exists(FilePath))
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(FilePath));
                var r = doc.RootElement;
                if (r.TryGetProperty("widgets", out var ws) && ws.ValueKind == JsonValueKind.Array)
                {
                    foreach (var w in ws.EnumerateArray())
                    {
                        var s = new WidgetSettings();
                        if (w.TryGetProperty("folderPath", out var fp) && fp.ValueKind == JsonValueKind.String && Directory.Exists(fp.GetString()))
                            s.FolderPath = fp.GetString()!;
                        if (w.TryGetProperty("x", out var x) && x.TryGetDouble(out var xd)) s.X = xd;
                        if (w.TryGetProperty("y", out var y) && y.TryGetDouble(out var yd)) s.Y = yd;
                        if (w.TryGetProperty("width", out var wd) && wd.TryGetDouble(out var wdv)) s.Width = wdv;
                        if (w.TryGetProperty("height", out var ht) && ht.TryGetDouble(out var htv)) s.Height = htv;
                        if (w.TryGetProperty("gridMode", out var gm) && gm.TryGetInt32(out var gmv) && (gmv == 2 || gmv == 3)) s.GridMode = gmv;
                        if (w.TryGetProperty("hideExtensions", out var he))
                            s.HideExtensions = he.ValueKind is JsonValueKind.True or JsonValueKind.False && he.GetBoolean();
                        if (w.TryGetProperty("positions", out var ps) && ps.ValueKind == JsonValueKind.Object)
                        {
                            foreach (var p in ps.EnumerateObject())
                            {
                                if (p.Value.ValueKind != JsonValueKind.Array || p.Value.GetArrayLength() != 2) continue;
                                try { s.Positions[p.Name] = new[] { p.Value[0].GetDouble(), p.Value[1].GetDouble() }; }
                                catch { }
                            }
                        }
                        if (string.IsNullOrEmpty(s.FolderPath) || !Directory.Exists(s.FolderPath))
                            s.FolderPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                        Widgets.Add(s);
                    }
                }
            }
        }
        catch { }
        if (Widgets.Count == 0)
        {
            Widgets.Add(new WidgetSettings
            {
                FolderPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)
            });
        }
    }

    public static void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(
                new
                {
                    widgets = Widgets.Select(w => new
                    {
                        folderPath = w.FolderPath,
                        x = w.X,
                        y = w.Y,
                        width = w.Width,
                        height = w.Height,
                        gridMode = w.GridMode,
                        hideExtensions = w.HideExtensions,
                        positions = w.Positions
                    })
                },
                new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(FilePath, json);
        }
        catch { }
    }
}
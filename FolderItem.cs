using System.ComponentModel;
using System.Windows.Media.Imaging;

namespace HyperOSFolder;

public sealed class FolderItem : INotifyPropertyChanged
{
    public string Name { get; }
    public string DisplayName { get; }
    public string Path { get; }
    public bool IsFolder { get; }
    public bool IsUrl { get; }
    public string? Url { get; }
    public string? IconFile { get; }
    public double PosX { get; set; }
    public double PosY { get; set; }

    private BitmapSource? _icon;
    public BitmapSource? Icon
    {
        get => _icon;
        set
        {
            _icon = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Icon)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public FolderItem(string name, string displayName, string path, bool isFolder,
        bool isUrl = false, string? url = null, string? iconFile = null)
    {
        Name = name;
        DisplayName = displayName;
        Path = path;
        IsFolder = isFolder;
        IsUrl = isUrl;
        Url = url;
        IconFile = iconFile;
    }
}

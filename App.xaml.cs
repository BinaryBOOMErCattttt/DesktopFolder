using System.Windows;

namespace DesktopFolder;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        Config.Load();
        foreach (var s in Config.Widgets)
            new MainWindow(s).Show();
    }
}
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Microsoft.Win32;
using MediaColor = System.Windows.Media.Color;
using Cursors = System.Windows.Input.Cursors;

namespace DesktopFolder;

public partial class MainWindow : Window
{
    private const double ExpandedWidth = 660;
    private const double ExpandedHeight = 830;
    private const double MinAWidth = 98;
    private const double MinAHeight = 126;
    private const double TileSize = 42;
    private const double BRowH = 118;
    private const double BBaseH = 76 + 4 + 20 + 36;
    private const double BPanelMarginX = 36 + 48;
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    private readonly ObservableCollection<FolderItem> _items = new();
    private readonly WidgetSettings _settings;
    private readonly DispatcherTimer _saveTimer;

    private string _folderPath = "";
    private bool _expanded;
    private bool _animating;
    private bool _suppressCollapse;

    private bool _dragging;
    private bool _dragMoved;
    private Point _dragStartScreen;
    private double _dragWinLeftPx;
    private double _dragWinTopPx;

    private FolderItem? _dragItem;
    private Button? _dragButton;
    private Point _dragItemStartScreen;
    private bool _justDragged;
    private bool _itemDragActive;
    private bool _cancelDrop;
    private double _itemOrigCanvasX;
    private double _itemOrigCanvasY;
    private bool _dragMovePending;
    private double _dragMoveLeft;
    private double _dragMoveTop;

    private bool _resizing;
    private int _resizeHit;
    private double _resizeStartX;
    private double _resizeStartY;
    private int _resizeRectL;
    private int _resizeRectT;
    private int _resizeRectR;
    private int _resizeRectB;

    private const int HIT_LEFT = 1;
    private const int HIT_RIGHT = 2;
    private const int HIT_TOP = 4;
    private const int HIT_BOTTOM = 8;

    private const double FrostBand = 14;
    private const double ResizeInset = 6;
    private const double BottomBand = 42 + ResizeInset;
    private const double CornerArc = 34;

    private FolderItem? _pressMiniItem;

    public MainWindow() : this(null) { }

    internal MainWindow(WidgetSettings? settings)
    {
        _settings = settings ?? Config.Primary;
        InitializeComponent();

        ShowInTaskbar = false;
        Title = L10n.Get("桌面文件夹", "Desktop Folder");
        _folderPath = _settings.FolderPath;
        ItemsList.ItemsSource = _items;
        ApplyL10n();

        var def = GetDefaultSize(_settings.GridMode);
        Width = Math.Clamp(_settings.Width ?? def.W, MinAWidth, SystemParameters.WorkArea.Width / 2);
        Height = Math.Clamp(_settings.Height ?? def.H, MinAHeight, SystemParameters.WorkArea.Height / 2);

        Point pos;
        if (_settings.HasPosition)
            pos = ClampToWorkArea(new Point(_settings.X!.Value, _settings.Y!.Value));
        else
        {
            var wa = SystemParameters.WorkArea;
            pos = new Point(wa.Right - Width - 28, wa.Top + 140);
        }
        Left = pos.X;
        Top = pos.Y;

        _saveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _saveTimer.Tick += (_, _) => { _saveTimer.Stop(); Config.Save(); };

        ShellIcon.SetupBackdrop(this);
        UpdateFolderTitle();
        _ = RefreshAsync();
    }

    private static (double W, double H) GetDefaultSize(int mode) => mode == 2 ? (140, 168) : (196, 224);

    private void ApplyL10n()
    {
        Title = L10n.Get("桌面文件夹", "Desktop Folder");
        HeaderGear.ToolTip = L10n.Get("菜单", "Menu");
        MenuOpen.Header = L10n.Get("打开文件夹", "Open Folder");
        MenuChange.Header = L10n.Get("更换文件夹…", "Change Folder…");
        MenuAutoStart.Header = L10n.Get("开机自启动", "Auto-start");
        MenuNew.Header = L10n.Get("新建小组件", "New Widget");
        MenuRefresh.Header = L10n.Get("刷新", "Refresh");
        MenuMode.Header = L10n.Get("显示模式", "Display Mode");
        MenuLang.Header = L10n.Get("语言", "Language");
        MenuHideExt.Header = L10n.Get("隐藏文件后缀名", "Hide Extensions");
        MenuDelete.Header = L10n.Get("删除小组件", "Delete Widget");
        MenuExit.Header = L10n.Get("退出", "Exit");
        EmptyHint.Text = L10n.Get("文件夹为空", "Folder is empty");
        MenuLangZh.IsChecked = !L10n.IsEn;
        MenuLangEn.IsChecked = L10n.IsEn;
        UpdateItemCount();
    }

    private void MenuLang_Click(object sender, RoutedEventArgs e)
    {
        L10n.Language = sender == MenuLangEn ? "en" : "zh";
        Config.Language = L10n.Language;
        Config.Save();
        foreach (Window w in Application.Current.Windows)
            if (w is MainWindow mw) mw.ApplyL10n();
    }

    private void UpdateItemCount()
    {
        ItemCountText.Text = L10n.Get($"{_items.Count} 项", $"{_items.Count} items");
    }

    private (double W, double H) ComputeExpandedSize()
    {
        double w = Math.Min(ExpandedWidth, SystemParameters.WorkArea.Width - 44);
        double h = Math.Min(ExpandedHeight, SystemParameters.WorkArea.Height - 44);
        return (w, h);
    }

    private double AWidth => Math.Clamp(_settings.Width ?? GetDefaultSize(_settings.GridMode).W, MinAWidth, SystemParameters.WorkArea.Width / 2);

    private double AHeight => Math.Clamp(_settings.Height ?? GetDefaultSize(_settings.GridMode).H, MinAHeight, SystemParameters.WorkArea.Height / 2);

    private Point ClampToWorkArea(Point p)
    {
        var wa = SystemParameters.WorkArea;
        return new Point(
            Math.Clamp(p.X, wa.Left, Math.Max(wa.Left, wa.Right - Width)),
            Math.Clamp(p.Y, wa.Top, Math.Max(wa.Top, wa.Bottom - Height)));
    }

    private Point ClampPx(double x, double y)
    {
        var wa = SystemParameters.WorkArea;
        var dpi = VisualTreeHelper.GetDpi(this);
        double s = dpi.DpiScaleX;
        return new Point(
            Math.Clamp(x, wa.Left * s, Math.Max(wa.Left * s, wa.Right * s - Width * s)),
            Math.Clamp(y, wa.Top * s, Math.Max(wa.Top * s, wa.Bottom * s - Height * s)));
    }

    private void UpdateFolderTitle()
    {
        var name = "";
        try
        {
            name = Path.GetFileName(_folderPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        }
        catch { }
        if (string.IsNullOrEmpty(name)) name = _folderPath;
        if (string.IsNullOrEmpty(name)) name = L10n.Get("文件夹", "Folder");
        FolderNameLabel.Text = name;
        ExpandedName.Text = name;
    }

    private async Task RefreshAsync()
    {
        _items.Clear();
        ItemCountText.Text = "";
        var entries = new List<(string Name, string Full, bool Folder)>();
        try
        {
            foreach (var d in Directory.EnumerateDirectories(_folderPath))
            {
                var a = File.GetAttributes(d);
                if ((a & (FileAttributes.Hidden | FileAttributes.System)) != 0) continue;
                entries.Add((Path.GetFileName(d), d, true));
            }
            foreach (var f in Directory.EnumerateFiles(_folderPath))
            {
                var a = File.GetAttributes(f);
                if ((a & (FileAttributes.Hidden | FileAttributes.System)) != 0) continue;
                entries.Add((Path.GetFileName(f), f, false));
            }
        }
        catch { }

        entries = entries
            .OrderByDescending(e => e.Folder)
            .ThenBy(e => e.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        var (bw, bh) = ComputeExpandedSize();
        double contentW = bw - BPanelMarginX;
        double contentH = bh - BBaseH;
        const double itemW = 104, itemH = 110;
        int n = Math.Max(1, entries.Count);
        int cols = Math.Min(5, n);
        int rows = (int)Math.Ceiling(n / (double)cols);
        double cellW = contentW / cols;
        double cellH = Math.Max(BRowH, contentH / rows);
        int col = 0, row = 0;
        foreach (var e in entries)
        {
            bool isUrl = false;
            string? url = null;
            string? iconFile = null;
            if (!e.Folder) isUrl = IsUrlFile(e.Full, out url, out iconFile);
            var display = GetDisplayName(e.Name, e.Folder, _settings.HideExtensions);
            var item = new FolderItem(e.Name, display, e.Full, e.Folder, isUrl, url, iconFile);
            if (_settings.Positions.TryGetValue(item.Path, out var pos))
            {
                item.PosX = Math.Clamp(pos[0], 0, Math.Max(0, contentW - itemW));
                item.PosY = Math.Clamp(pos[1], 0, Math.Max(0, contentH - itemH));
            }
            else
            {
                item.PosX = col * cellW + (cellW - itemW) / 2;
                item.PosY = row * cellH + (cellH - itemH) / 2;
                if (++col >= cols) { col = 0; row++; }
            }
            _items.Add(item);
        }
        ItemsList.Height = rows * cellH;
        UpdateItemCount();
        EmptyHint.Visibility = _items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        UpdateFolderTitle();
        UpdateMiniGrid();

        var keepKeys = new HashSet<string>();
        var livePaths = new HashSet<string>();
        foreach (var item in _items)
        {
            keepKeys.Add((item.IsFolder ? "D|" : "F|") + item.Path);
            livePaths.Add(item.Path);
        }
        ShellIcon.PruneCache(keepKeys);
        foreach (var k in _settings.Positions.Keys.ToList())
            if (!livePaths.Contains(k))
                _settings.Positions.Remove(k);

        foreach (var item in _items)
            _ = LoadIconAsync(item);
    }

    private async Task LoadIconAsync(FolderItem item)
    {
        try
        {
            var iconPath = item.IconFile != null && File.Exists(item.IconFile) ? item.IconFile : item.Path;
            var icon = await Task.Run(() => ShellIcon.GetLargeIcon(iconPath, item.IsFolder));
            if (icon == null) return;
            item.Icon = icon;
        }
        catch { }
    }

    private static bool IsUrlFile(string path, out string? url, out string? iconFile)
    {
        url = null;
        iconFile = null;
        var ext = Path.GetExtension(path);
        if (!ext.Equals(".url", StringComparison.OrdinalIgnoreCase) &&
            !ext.Equals(".website", StringComparison.OrdinalIgnoreCase))
            return false;

        string[]? lines = null;
        try { lines = File.ReadAllLines(path); }
        catch { }

        if (lines != null)
        {
            foreach (var raw in lines)
            {
                var t = raw.Trim();
                if (url == null && t.StartsWith("URL=", StringComparison.OrdinalIgnoreCase))
                    url = t[4..].Trim();
                else if (iconFile == null && t.StartsWith("IconFile=", StringComparison.OrdinalIgnoreCase))
                    iconFile = t[9..].Trim().Trim('"');
            }
        }
        if (!string.IsNullOrEmpty(iconFile) && !Path.IsPathRooted(iconFile))
            iconFile = Path.Combine(Path.GetDirectoryName(path) ?? "", iconFile);
        return true;
    }

    private static string GetDisplayName(string name, bool isFolder, bool hideExtensions)
    {
        if (!hideExtensions || isFolder) return name;
        var ext = Path.GetExtension(name);
        return string.IsNullOrEmpty(ext) ? name : name[..^ext.Length];
    }

    private (int Cols, int Rows) GridDims()
    {
        double w = Math.Max(0, WidgetBox.ActualWidth - 20);
        double h = Math.Max(0, WidgetBox.ActualHeight - 20);
        return (Math.Max(1, (int)(w / TileSize)), Math.Max(1, (int)(h / TileSize)));
    }

    private int VisibleCapInA()
    {
        var (cols, rows) = GridDims();
        return cols * rows;
    }

    private bool CanExpandToB() => _items.Count > VisibleCapInA();

    private void UpdateMiniGrid()
    {
        MiniArea.Clip = new RectangleGeometry(
            new Rect(0, 0, MiniArea.ActualWidth, MiniArea.ActualHeight), 16, 16);
        var (cols, rows) = GridDims();
        MiniGrid.Rows = rows;
        MiniGrid.Columns = cols;
        var cap = cols * rows;
        while (MiniGrid.Children.Count > cap)
            MiniGrid.Children.RemoveAt(MiniGrid.Children.Count - 1);
        for (int i = 0; i < Math.Min(cap, _items.Count); i++)
        {
            Border b;
            Image img;
            if (i < MiniGrid.Children.Count)
            {
                b = (Border)MiniGrid.Children[i];
                img = (Image)b.Child;
            }
            else
            {
                b = new Border
                {
                    CornerRadius = new CornerRadius(10),
                    Margin = new Thickness(2),
                    Width = 38,
                    Height = 38,
                    BorderThickness = new Thickness(1.5),
                    BorderBrush = new SolidColorBrush(MediaColor.FromArgb(0x00, 0x4C, 0x9A, 0xFF)),
                    Background = new SolidColorBrush(MediaColor.FromArgb(0xF2, 0xEF, 0xF3, 0xF8)),
                    RenderTransformOrigin = new Point(0.5, 0.5),
                    RenderTransform = new ScaleTransform(1, 1)
                };
                b.MouseEnter += Mini_MouseEnter;
                b.MouseLeave += Mini_MouseLeave;
                img = new Image { Width = 34, Height = 34 };
                RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);
                b.Child = img;
                MiniGrid.Children.Add(b);
            }
            b.Tag = _items[i];
            img.SetBinding(Image.SourceProperty,
                new System.Windows.Data.Binding(nameof(FolderItem.Icon)) { Source = _items[i] });
        }
    }

    private async void ToggleExpand()
    {
        if (_animating) return;
        if (!_expanded && !CanExpandToB()) return;
        _animating = true;
        try
        {
            if (_expanded) await CollapseAsync();
            else await ExpandAsync();
        }
        finally { _animating = false; }
    }

    private async Task ExpandAsync()
    {
        _expanded = true;
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        var wa = SystemParameters.WorkArea;
        var (bW, bH) = ComputeExpandedSize();
        var nl = Math.Clamp(Left + AWidth / 2 - bW / 2, wa.Left + 8, Math.Max(wa.Left + 8, wa.Right - bW - 8));
        var nt = Math.Clamp(Top + AHeight / 2 - bH / 2, wa.Top + 8, Math.Max(wa.Top + 8, wa.Bottom - bH - 8));

        FolderPanel.Visibility = Visibility.Visible;
        FolderPanel.Opacity = 0;
        FolderScale.ScaleX = 0.94;
        FolderScale.ScaleY = 0.94;
        FolderShift.Y = 26;

        Animate(this, LeftProperty, nl, 260, ease);
        Animate(this, TopProperty, nt, 260, ease);
        Animate(this, WidthProperty, bW, 260, ease);
        Animate(this, HeightProperty, bH, 260, ease);
        Animate(WidgetPanel, UIElement.OpacityProperty, 0, 150, ease, 120);
        Animate(FolderPanel, UIElement.OpacityProperty, 1, 240, ease, 170);
        Animate(FolderScale, ScaleTransform.ScaleXProperty, 1, 300, ease, 170);
        Animate(FolderScale, ScaleTransform.ScaleYProperty, 1, 300, ease, 170);
        Animate(FolderShift, TranslateTransform.YProperty, 0, 300, ease, 170);

        await Task.Delay(440);
        WidgetPanel.Visibility = Visibility.Collapsed;
        WidgetPanel.BeginAnimation(UIElement.OpacityProperty, null);
        WidgetPanel.Opacity = 0;
        Focus();

        _ = RefreshAsync();
    }

    private async Task CollapseAsync()
    {
        if (!_expanded) return;
        _expanded = false;
        var ease = new CubicEase { EasingMode = EasingMode.EaseInOut };

        Animate(FolderPanel, UIElement.OpacityProperty, 0, 130);
        Animate(FolderScale, ScaleTransform.ScaleXProperty, 0.96, 130);
        Animate(FolderScale, ScaleTransform.ScaleYProperty, 0.96, 130);

        WidgetPanel.Visibility = Visibility.Visible;
        WidgetPanel.Opacity = 0;
        var aW = AWidth;
        var aH = AHeight;
        Animate(WidgetPanel, UIElement.OpacityProperty, 1, 200, ease, 90);
        Animate(this, LeftProperty, Left + (Width - aW) / 2, 240, ease, 60);
        Animate(this, TopProperty, Top + (Height - aH) / 2, 240, ease, 60);
        Animate(this, WidthProperty, aW, 240, ease, 60);
        Animate(this, HeightProperty, aH, 240, ease, 60);

        await Task.Delay(330);

        BeginAnimation(LeftProperty, null);
        BeginAnimation(TopProperty, null);
        BeginAnimation(WidthProperty, null);
        BeginAnimation(HeightProperty, null);
        WidgetPanel.BeginAnimation(UIElement.OpacityProperty, null);
        FolderPanel.BeginAnimation(UIElement.OpacityProperty, null);
        FolderScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        FolderScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        FolderShift.BeginAnimation(TranslateTransform.YProperty, null);
        Left = ClampToWorkArea(new Point(Left, Top)).X;
        Top = ClampToWorkArea(new Point(Left, Top)).Y;
        Width = aW;
        Height = aH;
        UpdateMiniGrid();
        FolderPanel.Visibility = Visibility.Collapsed;
        WidgetPanel.Opacity = 1;
        SavePosition();
    }

    private void Animate(IAnimatable target, DependencyProperty dp, double to, double ms, IEasingFunction? ease = null, int delayMs = 0)
    {
        var a = new DoubleAnimation(to, TimeSpan.FromMilliseconds(ms))
        {
            EasingFunction = ease ?? new QuadraticEase { EasingMode = EasingMode.EaseInOut }
        };
        if (delayMs > 0) a.BeginTime = TimeSpan.FromMilliseconds(delayMs);
        target.BeginAnimation(dp, a);
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        ShellIcon.AttachToDesktop(new WindowInteropHelper(this).Handle);
    }

    private void SavePosition()
    {
        _settings.X = Left;
        _settings.Y = Top;
        Config.Save();
    }

    // ---- events ----

    private void WidgetBox_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragging = true;
        _dragMoved = false;
        _pressMiniItem = FindMiniItem(e.OriginalSource as DependencyObject);
        var cur = ShellIcon.GetCursorPosScreen();
        _dragStartScreen = new Point(cur.X, cur.Y);
        var r = ShellIcon.GetWindowRectPx(new WindowInteropHelper(this).Handle);
        _dragWinLeftPx = r.Left;
        _dragWinTopPx = r.Top;
        CaptureMouse();
        CompositionTarget.Rendering += OnDragFrame;
        ShellIcon.DisableBackdrop(new WindowInteropHelper(this).Handle);
        e.Handled = true;
    }

    private void Item_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _justDragged = false;
        if (sender is Button b && b.Tag is FolderItem it)
        {
            _dragItem = it;
            _dragButton = b;
            var cur = ShellIcon.GetCursorPosScreen();
            _dragItemStartScreen = new Point(cur.X, cur.Y);
        }
    }

    private void BeginItemDrag()
    {
        _itemDragActive = true;
        _justDragged = true;
        _cancelDrop = false;
        if (_dragButton != null)
        {
            _itemOrigCanvasX = Canvas.GetLeft(_dragButton);
            _itemOrigCanvasY = Canvas.GetTop(_dragButton);
            _dragButton.RenderTransformOrigin = new Point(0.5, 0.5);
            _dragButton.RenderTransform = new ScaleTransform(1.08, 1.08);
        }
        CaptureMouse();
        _suppressCollapse = true;
    }

    private void OnDragFrame(object? sender, EventArgs e)
    {
        if ((!_itemDragActive && !_dragging) || !_dragMovePending) return;
        _dragMovePending = false;
        ShellIcon.PositionWindow(new WindowInteropHelper(this).Handle, (int)_dragMoveLeft, (int)_dragMoveTop);
    }

    private void EndItemDrag(MouseButtonEventArgs e)
    {
        var item = _dragItem;
        var btn = _dragButton;
        bool dropped = _itemDragActive && !_cancelDrop && item != null;
        _itemDragActive = false;
        _dragItem = null;
        _dragButton = null;
        ReleaseMouseCapture();
        _suppressCollapse = false;

        if (btn != null) btn.RenderTransform = null;

        if (!dropped)
        {
            if (item != null && btn != null)
            {
                Canvas.SetLeft(btn, _itemOrigCanvasX);
                Canvas.SetTop(btn, _itemOrigCanvasY);
            }
            return;
        }

        var r = ShellIcon.GetWindowRectPx(new WindowInteropHelper(this).Handle);
        var cur = ShellIcon.GetCursorPosScreen();
        bool inside = cur.X >= r.Left && cur.Y >= r.Top && cur.X <= r.Right && cur.Y <= r.Bottom;

        if (inside)
        {
            if (item != null && btn != null)
            {
                item.PosX = Canvas.GetLeft(btn);
                item.PosY = Canvas.GetTop(btn);
                _settings.Positions[item.Path] = new[] { item.PosX, item.PosY };
                Config.Save();
            }
        }
        else
        {
            bool copy = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
            var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            try { MoveOrCopy(item!.Path, desktop, !copy); }
            catch { }
            _ = RefreshAsync();
        }
    }

    private void Window_MouseMove(object sender, MouseEventArgs e)
    {
        if (_dragItem != null)
        {
            var cur = ShellIcon.GetCursorPosScreen();
            if (!_itemDragActive)
            {
                if (Math.Abs(cur.X - _dragItemStartScreen.X) <= 6 && Math.Abs(cur.Y - _dragItemStartScreen.Y) <= 6) return;
                BeginItemDrag();
            }
            if (_itemDragActive && _dragButton != null)
            {
                var dpi = VisualTreeHelper.GetDpi(this);
                double idx = (cur.X - _dragItemStartScreen.X) / dpi.DpiScaleX;
                double idy = (cur.Y - _dragItemStartScreen.Y) / dpi.DpiScaleY;
                double maxX = Math.Max(0, ItemsList.ActualWidth - 104);
                double maxY = Math.Max(0, ItemsList.ActualHeight - 110);
                Canvas.SetLeft(_dragButton, Math.Clamp(_itemOrigCanvasX + idx, 0, maxX));
                Canvas.SetTop(_dragButton, Math.Clamp(_itemOrigCanvasY + idy, 0, maxY));
            }
            return;
        }
        if (!_dragging) return;
        var pos = ShellIcon.GetCursorPosScreen();
        var dx = pos.X - _dragStartScreen.X;
        var dy = pos.Y - _dragStartScreen.Y;
        if (!_dragMoved && (Math.Abs(dx) > 6 || Math.Abs(dy) > 6)) _dragMoved = true;
        if (_dragMoved)
        {
            var q = ClampPx(_dragWinLeftPx + dx, _dragWinTopPx + dy);
            _dragMoveLeft = q.X;
            _dragMoveTop = q.Y;
            _dragMovePending = true;
        }
    }

    private void Window_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_itemDragActive)
        {
            EndItemDrag(e);
            return;
        }
        _dragItem = null;
        if (!_dragging) return;
        _dragging = false;
        ReleaseMouseCapture();
        CompositionTarget.Rendering -= OnDragFrame;
        ShellIcon.EnableBackdrop(new WindowInteropHelper(this).Handle);
        if (_dragMoved)
        {
            var dpi = VisualTreeHelper.GetDpi(this);
            Left = _dragMoveLeft / dpi.DpiScaleX;
            Top = _dragMoveTop / dpi.DpiScaleY;
            UpdateLayout();
            InvalidateVisual();
            SavePosition();
        }
        else if (_pressMiniItem != null)
        {
            var it = _pressMiniItem;
            _pressMiniItem = null;
            OpenItem(it);
        }
        else ToggleExpand();
    }

    private void Mini_MouseEnter(object sender, MouseEventArgs e)
    {
        if (sender is not Border b) return;
        AnimateMini(b, 1.15,
            MediaColor.FromArgb(0x33, 0x4C, 0x9A, 0xFF),
            MediaColor.FromArgb(0xB3, 0x4C, 0x9A, 0xFF));
    }

    private void Mini_MouseLeave(object sender, MouseEventArgs e)
    {
        if (sender is not Border b) return;
        AnimateMini(b, 1.0,
            MediaColor.FromArgb(0xF2, 0xEF, 0xF3, 0xF8),
            MediaColor.FromArgb(0x00, 0x4C, 0x9A, 0xFF));
    }

    private static void AnimateMini(Border b, double scale, MediaColor bg, MediaColor ring)
    {
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        if (b.RenderTransform is ScaleTransform st)
        {
            st.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(scale, TimeSpan.FromMilliseconds(150)) { EasingFunction = ease });
            st.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(scale, TimeSpan.FromMilliseconds(150)) { EasingFunction = ease });
        }
        if (b.Background is SolidColorBrush bgBrush)
            bgBrush.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation(bg, TimeSpan.FromMilliseconds(150)) { EasingFunction = ease });
        if (b.BorderBrush is SolidColorBrush ringBrush)
            ringBrush.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation(ring, TimeSpan.FromMilliseconds(150)) { EasingFunction = ease });
    }

    private static FolderItem? FindMiniItem(DependencyObject? d)
    {
        while (d != null)
        {
            if (d is Border { Tag: FolderItem fi }) return fi;
            d = VisualTreeHelper.GetParent(d);
        }
        return null;
    }

    private DateTime _lastOpenTime = DateTime.MinValue;

    private void OpenItem(FolderItem it)
    {
        var now = DateTime.Now;
        if ((now - _lastOpenTime).TotalMilliseconds < 600) return;
        _lastOpenTime = now;
        var target = it.Url ?? it.Path;
        try
        {
            Process.Start(new ProcessStartInfo(target)
            {
                UseShellExecute = true,
                WorkingDirectory = Path.GetDirectoryName(it.Path) ?? _folderPath
            });
        }
        catch { }
    }

    // ---- A 状态手动边缘缩放(仿原生窗口) ----

    private int HitTestResize(Point p)
    {
        if (_expanded || _animating || _itemDragActive || _dragging || _resizing) return 0;
        double w = ActualWidth, h = ActualHeight;
        double side = FrostBand + ResizeInset;
        int hit = 0;
        if (p.X <= side) hit |= HIT_LEFT;
        if (p.X >= w - side) hit |= HIT_RIGHT;
        if (p.Y <= side) hit |= HIT_TOP;
        if (p.Y >= h - BottomBand) hit |= HIT_BOTTOM;
        if (hit == 0) return 0;

        double dx = Math.Min(p.X, w - p.X);
        double dy = Math.Min(p.Y, h - p.Y);
        if (dx <= CornerArc && dy <= CornerArc && dx * dx + dy * dy <= CornerArc * CornerArc)
        {
            if (dx == p.X) hit |= HIT_LEFT; else hit |= HIT_RIGHT;
            if (dy == p.Y) hit |= HIT_TOP; else hit |= HIT_BOTTOM;
        }
        return hit;
    }

    private void ApplyCapLimit(ref int l, ref int t, ref int r, ref int b, DpiScale dpi)
    {
        int C = _items.Count;
        if (C <= 0) return;
        double sx = dpi.DpiScaleX, sy = dpi.DpiScaleY;
        double curW = (_resizeRectR - _resizeRectL) / sx;
        double curH = (_resizeRectB - _resizeRectT) / sy;
        int curCols = Math.Max(1, (int)((curW - 48) / 42));
        int curRows = Math.Max(1, (int)((curH - 48) / 42));
        if (curCols * curRows >= C)
        {
            if ((_resizeHit & HIT_LEFT) != 0) l = Math.Max(l, _resizeRectL);
            if ((_resizeHit & HIT_RIGHT) != 0) r = Math.Min(r, _resizeRectR);
            if ((_resizeHit & HIT_TOP) != 0) t = Math.Max(t, _resizeRectT);
            if ((_resizeHit & HIT_BOTTOM) != 0) b = Math.Min(b, _resizeRectB);
            return;
        }
        double wDip = (r - l) / sx;
        double hDip = (b - t) / sy;
        int rows2 = Math.Max(1, (int)((hDip - 48) / 42));
        int cols2 = Math.Max(1, (int)((wDip - 48) / 42));
        int colsT = (int)Math.Ceiling(C / (double)rows2);
        int rowsT = (int)Math.Ceiling(C / (double)cols2);
        double wMaxPx = (48 + (colsT + 1) * 42 - 1) * sx;
        double hMaxPx = (48 + (rowsT + 1) * 42 - 1) * sy;
        if ((_resizeHit & HIT_LEFT) != 0) l = Math.Max(l, (int)(r - wMaxPx));
        if ((_resizeHit & HIT_RIGHT) != 0) r = Math.Min(r, (int)(l + wMaxPx));
        if ((_resizeHit & HIT_TOP) != 0) t = Math.Max(t, (int)(b - hMaxPx));
        if ((_resizeHit & HIT_BOTTOM) != 0) b = Math.Min(b, (int)(t + hMaxPx));
    }

    private void Window_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (_resizing)
        {
            var cur = ShellIcon.GetCursorPosScreen();
            var dpi = VisualTreeHelper.GetDpi(this);
            double minW = MinAWidth * dpi.DpiScaleX;
            double minH = MinAHeight * dpi.DpiScaleY;
            double maxW = SystemParameters.WorkArea.Width / 2 * dpi.DpiScaleX;
            double maxH = SystemParameters.WorkArea.Height / 2 * dpi.DpiScaleY;
            double dx = cur.X - _resizeStartX;
            double dy = cur.Y - _resizeStartY;
            int l = _resizeRectL, t = _resizeRectT, r = _resizeRectR, b = _resizeRectB;
            if ((_resizeHit & HIT_LEFT) != 0)
                l = (int)Math.Clamp(l + dx, r - maxW, r - minW);
            if ((_resizeHit & HIT_RIGHT) != 0)
                r = (int)Math.Clamp(r + dx, l + minW, l + maxW);
            if ((_resizeHit & HIT_TOP) != 0)
                t = (int)Math.Clamp(t + dy, b - maxH, b - minH);
            if ((_resizeHit & HIT_BOTTOM) != 0)
                b = (int)Math.Clamp(b + dy, t + minH, t + maxH);
            ApplyCapLimit(ref l, ref t, ref r, ref b, dpi);
            ShellIcon.ResizeWindow(new WindowInteropHelper(this).Handle, l, t, r - l, b - t);
            e.Handled = true;
            return;
        }

        int hit = HitTestResize(e.GetPosition(this));
        Cursor = hit switch
        {
            HIT_LEFT => Cursors.SizeWE,
            HIT_RIGHT => Cursors.SizeWE,
            HIT_TOP => Cursors.SizeNS,
            HIT_BOTTOM => Cursors.SizeNS,
            HIT_LEFT | HIT_TOP => Cursors.SizeNWSE,
            HIT_RIGHT | HIT_BOTTOM => Cursors.SizeNWSE,
            HIT_RIGHT | HIT_TOP => Cursors.SizeNESW,
            HIT_LEFT | HIT_BOTTOM => Cursors.SizeNESW,
            _ => _expanded ? Cursors.Arrow : Cursors.Hand
        };
    }

    private void Window_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        int hit = HitTestResize(e.GetPosition(this));
        if (hit == 0) return;
        _resizing = true;
        _resizeHit = hit;
        var cur = ShellIcon.GetCursorPosScreen();
        _resizeStartX = cur.X;
        _resizeStartY = cur.Y;
        var r = ShellIcon.GetWindowRectPx(new WindowInteropHelper(this).Handle);
        _resizeRectL = r.Left;
        _resizeRectT = r.Top;
        _resizeRectR = r.Right;
        _resizeRectB = r.Bottom;
        CaptureMouse();
        ShellIcon.DisableBackdrop(new WindowInteropHelper(this).Handle);
        e.Handled = true;
    }

    private void Window_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_resizing) return;
        _resizing = false;
        ReleaseMouseCapture();
        ShellIcon.EnableBackdrop(new WindowInteropHelper(this).Handle);
        var dpi = VisualTreeHelper.GetDpi(this);
        var r = ShellIcon.GetWindowRectPx(new WindowInteropHelper(this).Handle);
        Left = r.Left / dpi.DpiScaleX;
        Top = r.Top / dpi.DpiScaleY;
        Width = (r.Right - r.Left) / dpi.DpiScaleX;
        Height = (r.Bottom - r.Top) / dpi.DpiScaleY;
        UpdateLayout();
        InvalidateVisual();
        SavePosition();
        e.Handled = true;
    }

    // ---- 从资源管理器拖入本文件夹 ----

    private void Panel_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
            ? DragDropEffects.Copy | DragDropEffects.Move
            : DragDropEffects.None;
        e.Handled = true;
    }

    private void Panel_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] files)
        {
            bool move = (e.Effects & DragDropEffects.Move) == DragDropEffects.Move;
            foreach (var f in files)
            {
                try { MoveOrCopy(f, _folderPath, move); }
                catch { }
            }
            _ = RefreshAsync();
        }
        e.Handled = true;
    }

    private static void MoveOrCopy(string src, string destDir, bool move)
    {
        var name = Path.GetFileName(src);
        if (string.IsNullOrEmpty(name)) return;
        var dest = Path.Combine(destDir, name);
        if (File.Exists(dest) || Directory.Exists(dest))
        {
            var ext = Path.GetExtension(name);
            var stem = Path.GetFileNameWithoutExtension(name);
            for (int i = 1; ; i++)
            {
                dest = i <= 99
                    ? Path.Combine(destDir, $"{stem} ({i}){ext}")
                    : Path.Combine(destDir, Guid.NewGuid().ToString("N") + ext);
                if (!File.Exists(dest) && !Directory.Exists(dest)) break;
            }
        }
        if (Directory.Exists(src))
        {
            if (move) Directory.Move(src, dest);
            else CopyDir(src, dest);
        }
        else
        {
            if (move) MoveFileRobust(src, dest);
            else File.Copy(src, dest);
        }
    }

    private static void MoveFileRobust(string src, string dest)
    {
        try { File.Move(src, dest); }
        catch (IOException) { File.Copy(src, dest, false); try { File.Delete(src); } catch { } }
        catch (UnauthorizedAccessException) { File.Copy(src, dest, false); try { File.Delete(src); } catch { } }
    }

    private static void CopyDir(string src, string dest)
    {
        Directory.CreateDirectory(dest);
        foreach (var f in Directory.EnumerateFiles(src))
            File.Copy(f, Path.Combine(dest, Path.GetFileName(f)), false);
        foreach (var d in Directory.EnumerateDirectories(src))
            CopyDir(d, Path.Combine(dest, Path.GetFileName(d)));
    }

    private void Window_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (!_expanded || _animating || _itemDragActive) return;
        if (e.OriginalSource is DependencyObject d && FolderPanel.IsAncestorOf(d)) return;
        _ = CollapseAsync();
    }

    private void FolderPanel_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (FindAncestor<Button>(e.OriginalSource as DependencyObject) != null) return;
        if (_animating) return;
        _dragging = true;
        _dragMoved = false;
        var cur = ShellIcon.GetCursorPosScreen();
        _dragStartScreen = new Point(cur.X, cur.Y);
        var r = ShellIcon.GetWindowRectPx(new WindowInteropHelper(this).Handle);
        _dragWinLeftPx = r.Left;
        _dragWinTopPx = r.Top;
        CaptureMouse();
        CompositionTarget.Rendering += OnDragFrame;
        ShellIcon.DisableBackdrop(new WindowInteropHelper(this).Handle);
        e.Handled = true;
    }

    private void Item_Click(object sender, RoutedEventArgs e)
    {
        if (_justDragged)
        {
            _justDragged = false;
            return;
        }
        if (sender is Button b && b.Tag is FolderItem it)
        {
            OpenItem(it);
        }
        ToggleExpand();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => ToggleExpand();

    private void ItemsScroller_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (e.Delta == 0) return;
        ItemsScroller.ScrollToVerticalOffset(ItemsScroller.VerticalOffset - e.Delta / 120.0 * 118);
        e.Handled = true;
    }

    private async void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            if (_itemDragActive)
            {
                _cancelDrop = true;
                return;
            }
            if (_dragging) return;
            if (_expanded && !_animating) await CollapseAsync();
        }
    }

    private void Window_Deactivated(object sender, EventArgs e)
    {
        if (_expanded && !_suppressCollapse && !ShellIcon.IsForegroundSameProcess())
            _ = CollapseAsync();
    }

    // ---- 齿轮菜单(仅展开状态显示) ----

    private void HeaderGear_Click(object sender, RoutedEventArgs e)
    {
        if (HeaderGear.ContextMenu == null) return;
        HeaderGear.ContextMenu.PlacementTarget = HeaderGear;
        HeaderGear.ContextMenu.IsOpen = true;
    }

    private void Menu_Opened(object sender, RoutedEventArgs e)
    {
        MenuAutoStart.IsChecked = IsAutoStartEnabled();
        MenuHideExt.IsChecked = _settings.HideExtensions;
        Mode2.IsChecked = _settings.GridMode == 2;
        Mode3.IsChecked = _settings.GridMode == 3;
        MenuLangZh.IsChecked = !L10n.IsEn;
        MenuLangEn.IsChecked = L10n.IsEn;
        _suppressCollapse = true;
    }

    private void Menu_Closed(object sender, RoutedEventArgs e) => _suppressCollapse = false;

    private void MenuOpen_Click(object sender, RoutedEventArgs e)
    {
        try { Process.Start(new ProcessStartInfo(_folderPath) { UseShellExecute = true }); }
        catch { }
    }

    private void MenuChange_Click(object sender, RoutedEventArgs e)
    {
        _suppressCollapse = true;
        try
        {
            using var dlg = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = L10n.Get("选择要在桌面文件夹中展示的目录", "Choose a folder to show in the widget"),
                SelectedPath = _folderPath,
                ShowNewFolderButton = false
            };
            if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK && Directory.Exists(dlg.SelectedPath))
            {
                _folderPath = dlg.SelectedPath;
                _settings.FolderPath = _folderPath;
                Config.Save();
                UpdateFolderTitle();
                _ = RefreshAsync();
            }
        }
        finally { _suppressCollapse = false; }
    }

    private void MenuRefresh_Click(object sender, RoutedEventArgs e) => _ = RefreshAsync();

    private void MenuAutoStart_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKey);
            if (key == null) return;
            if (MenuAutoStart.IsChecked)
                key.SetValue("DesktopFolder", $"\"{Environment.ProcessPath}\"");
            else
                key.DeleteValue("DesktopFolder", false);
        }
        catch { }
    }

    private void MenuNewWidget_Click(object sender, RoutedEventArgs e)
    {
        var s = new WidgetSettings
        {
            FolderPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            X = Left + 40,
            Y = Top + 40,
            HideExtensions = _settings.HideExtensions,
            GridMode = _settings.GridMode
        };
        Config.Widgets.Add(s);
        Config.Save();
        new MainWindow(s).Show();
    }

    private void MenuHideExt_Click(object sender, RoutedEventArgs e)
    {
        _settings.HideExtensions = MenuHideExt.IsChecked;
        Config.Save();
        _ = RefreshAsync();
    }

    private void Mode2_Click(object sender, RoutedEventArgs e) => SetGridMode(2);

    private void Mode3_Click(object sender, RoutedEventArgs e) => SetGridMode(3);

    private void SetGridMode(int mode)
    {
        if (mode != 2 && mode != 3) return;
        if (_settings.GridMode == mode) return;
        _settings.GridMode = mode;
        var def = GetDefaultSize(mode);
        _settings.Width = def.W;
        _settings.Height = def.H;
        if (!_expanded)
        {
            Width = def.W;
            Height = def.H;
            UpdateMiniGrid();
        }
        Config.Save();
    }

    private void MenuExit_Click(object sender, RoutedEventArgs e) => Close();

    private void MenuDeleteWidget_Click(object sender, RoutedEventArgs e)
    {
        if (Config.Widgets.Count <= 1)
        {
            System.Windows.MessageBox.Show(this,
                L10n.Get("至少需要保留一个小组件。", "At least one widget must remain."),
                L10n.Get("删除小组件", "Delete Widget"),
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var r = System.Windows.MessageBox.Show(this,
            L10n.Get("确定删除此小组件?\n它不会影响源文件夹。", "Delete this widget?\nThe source folder will not be affected."),
            L10n.Get("删除小组件", "Delete Widget"),
            MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (r != MessageBoxResult.Yes) return;
        Config.Widgets.Remove(_settings);
        Config.Save();
        Close();
    }

    private static bool IsAutoStartEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue("DesktopFolder") != null;
        }
        catch { return false; }
    }

    private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_settings == null) return;
        if (!_expanded && !_animating)
        {
            _settings.Width = ActualWidth;
            _settings.Height = ActualHeight;
            _saveTimer.Stop();
            _saveTimer.Start();
            UpdateMiniGrid();
        }
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        SavePosition();
        Config.Save();
        base.OnClosing(e);
    }

    private static T? FindAncestor<T>(DependencyObject? d) where T : DependencyObject
    {
        while (d != null)
        {
            if (d is T t) return t;
            d = VisualTreeHelper.GetParent(d) ?? LogicalTreeHelper.GetParent(d);
        }
        return null;
    }
}

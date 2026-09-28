using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Threading;
using System.Runtime.InteropServices;

namespace LuckerParty.Launcher;

internal sealed partial class LauncherWindow
{
    private Button? _maximizeButton;
    private WindowCaptionGlyph? _maximizeGlyph;

    private void ConfigureChrome()
    {
        ExtendClientAreaToDecorationsHint = true;
        ExtendClientAreaChromeHints = ExtendClientAreaChromeHints.NoChrome;
        ExtendClientAreaTitleBarHeightHint = 40;
        // Windows retains its native resize border, system menu and DWM rounding.
        // X11 needs BorderOnly because it can ignore the client-extension hint.
        SystemDecorations = OperatingSystem.IsWindows() ? SystemDecorations.Full : SystemDecorations.BorderOnly;
        CanResize = true;
        SizeChanged += (_, _) => UpdateHomeLayout();
        if (OperatingSystem.IsWindows())
        {
            Win32Properties.AddWndProcHookCallback(this, PreserveCloseMenu);
            Opened += (_, _) => RestoreCloseMenu();
            Closed += (_, _) => Win32Properties.RemoveWndProcHookCallback(this, PreserveCloseMenu);
        }
        PropertyChanged += (_, change) =>
        {
            if (change.Property == OffScreenMarginProperty) _shell.Margin = OffScreenMargin;
            if (change.Property == IsExtendedIntoWindowDecorationsProperty || change.Property == WindowStateProperty)
                RestoreCloseMenu();
            if (change.Property == WindowStateProperty)
            {
                var maximized = WindowState == WindowState.Maximized;
                if (_maximizeGlyph is not null) _maximizeGlyph.Restore = maximized;
                if (_maximizeButton is not null)
                {
                    var label = maximized ? "Restore window" : "Maximize window";
                    AutomationProperties.SetName(_maximizeButton, label);
                    ToolTip.SetTip(_maximizeButton, label);
                }
                UpdateHomeLayout();
            }
        };
        KeyDown += (_, args) =>
        {
            // NoChrome disables the native Close menu command in Avalonia 11;
            // route Alt+F4 through the same guarded Close action as our caption.
            if (args.Key == Key.F4 && args.KeyModifiers.HasFlag(KeyModifiers.Alt))
            {
                Close(); args.Handled = true;
            }
            else if (args.Key == Key.Escape && _settings.IsVisible)
            {
                _settings.IsVisible = false; _home.IsVisible = true;
                UpdateHomeLayout(); args.Handled = true;
            }
        };
    }

    private void RestoreCloseMenu()
    {
        if (!OperatingSystem.IsWindows()) return;
        // Avalonia may reset the menu after a state/DWM change. Restore it after
        // that operation, without interfering with its native window processing.
        Dispatcher.UIThread.Post(() =>
        {
            if (TryGetPlatformHandle() is { HandleDescriptor: "HWND" } handle)
                WindowsCloseMenu.Enable(handle.Handle);
        });
    }

    private static IntPtr PreserveCloseMenu(IntPtr window, uint message, IntPtr parameter, IntPtr data, ref bool handled)
    {
        const uint initializeMenu = 0x0116;
        if (message == initializeMenu) WindowsCloseMenu.Enable(window);
        return IntPtr.Zero;
    }

    private Control BuildCaptionRow()
    {
        var row = new Grid { ColumnDefinitions = new("*,Auto"), Height = 40 };
        var drag = new Border { Background = Brushes.Transparent };
        ConfigureDragRegion(drag); Place(row, drag);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal };
        var minimize = CaptionButton("Minimize window", "minimize", Win32Properties.Win32HitTestValue.MinButton);
        minimize.Click += (_, _) => WindowState = WindowState.Minimized;
        _maximizeGlyph = new WindowCaptionGlyph("maximize");
        _maximizeButton = CaptionButton("Maximize window", _maximizeGlyph, Win32Properties.Win32HitTestValue.MaxButton);
        _maximizeButton.Click += (_, _) => ToggleMaximize();
        var close = CaptionButton("Close launcher", "close", Win32Properties.Win32HitTestValue.Close);
        close.Classes.Add("caption-close"); close.Click += (_, _) => Close();
        buttons.Children.Add(minimize); buttons.Children.Add(_maximizeButton); buttons.Children.Add(close);
        Place(row, buttons, column: 1);
        return row;
    }

    private static Button CaptionButton(string name, object glyph, Win32Properties.Win32HitTestValue hitTest)
    {
        var icon = glyph as WindowCaptionGlyph ?? new WindowCaptionGlyph((string)glyph);
        icon.Width = icon.Height = 16;
        var button = Button(icon);
        button.Width = 46; button.Height = 40; button.Padding = new Thickness(15, 12);
        button.CornerRadius = new CornerRadius(0); button.Classes.Add("caption");
        AutomationProperties.SetName(button, name); ToolTip.SetTip(button, name);
        // Avalonia 11.3.22 translates non-client caption input back to button events.
        // MaxButton also exposes Windows 11's native Snap Layout hover affordance.
        Win32Properties.SetNonClientHitTestResult(button, hitTest);
        return button;
    }

    private void ToggleMaximize() => WindowState = WindowState == WindowState.Maximized
        ? WindowState.Normal : WindowState.Maximized;

    private void ConfigureDragRegion(Control region)
    {
        Win32Properties.SetNonClientHitTestResult(region, Win32Properties.Win32HitTestValue.Caption);
        region.PointerPressed += (_, args) =>
        {
            if (!args.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
            if (args.ClickCount == 2) ToggleMaximize();
            else BeginMoveDrag(args);
            args.Handled = true;
        };
    }
}

// Avalonia 11's NoChrome grays SC_CLOSE. Only restore that menu entry; the OS
// still routes it through Avalonia's WM_CLOSE and our normal Closing guard.
internal static class WindowsCloseMenu
{
    public static void Enable(IntPtr window)
    {
        const uint closeCommand = 0xF060, byCommandAndEnabled = 0;
        var menu = GetSystemMenu(window, false);
        if (menu != IntPtr.Zero) EnableMenuItem(menu, closeCommand, byCommandAndEnabled);
    }

    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern IntPtr GetSystemMenu(IntPtr window, [MarshalAs(UnmanagedType.Bool)] bool revert);

    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern uint EnableMenuItem(IntPtr menu, uint command, uint flags);
}

internal sealed class WindowCaptionGlyph(string kind) : Control
{
    private bool _restore;
    public bool Restore
    {
        get => _restore;
        set { if (_restore != value) { _restore = value; InvalidateVisual(); } }
    }

    public override void Render(DrawingContext context)
    {
        using var scale = context.PushTransform(Matrix.CreateScale(Bounds.Width / 16, Bounds.Height / 16));
        var pen = new Pen(PartyRoom.Ink, 1.3);
        if (kind == "minimize") context.DrawLine(pen, new Point(3, 9), new Point(13, 9));
        else if (kind == "close")
        {
            context.DrawLine(pen, new Point(3, 3), new Point(13, 13));
            context.DrawLine(pen, new Point(3, 13), new Point(13, 3));
        }
        else if (Restore)
        {
            context.DrawGeometry(null, pen, Geometry.Parse("M 5,5 L 5,2.5 L 13.5,2.5 L 13.5,11 L 11,11"));
            context.DrawRectangle(null, pen, new Rect(2.5, 5, 8.5, 8.5));
        }
        else context.DrawRectangle(null, pen, new Rect(3, 3, 10, 10));
    }
}

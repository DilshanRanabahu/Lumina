using Microsoft.Win32;

namespace Lumina;

sealed class TrayApp : ApplicationContext
{
    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    const string RunValue = "Lumina";

    readonly NotifyIcon tray;
    readonly BrightnessPopup popup = new();
    readonly ToolStripMenuItem startupItem;
    List<DisplayItem> displays = [];
    bool displaysChanged = true;

    public TrayApp()
    {
        startupItem = new ToolStripMenuItem("Start with Windows", null, (_, _) => ToggleStartup()) { Checked = IsStartupEnabled() };

        var menu = new ContextMenuStrip { Renderer = new DarkMenuRenderer(), BackColor = Theme.Card, ForeColor = Theme.Text, ShowImageMargin = true };
        menu.Items.Add(new ToolStripMenuItem("Refresh screens", null, (_, _) => displaysChanged = true));
        menu.Items.Add(startupItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem("Exit", null, (_, _) => ExitThread()));

        tray = new NotifyIcon
        {
            Icon = CreateSunIcon(),
            Text = "Lumina",
            ContextMenuStrip = menu,
            Visible = true,
        };
        tray.MouseClick += OnTrayClick;

        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
    }

    void OnDisplaySettingsChanged(object? sender, EventArgs e) => displaysChanged = true;

    void OnTrayClick(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left) return;

        // Clicking the icon while the popup is open deactivates it first; don't reopen it straight away.
        if ((DateTime.Now - popup.LastHidden).TotalMilliseconds < 300) return;

        if (displaysChanged)
        {
            foreach (var d in displays) d.Dispose();
            displays = DisplayManager.Enumerate();
            displaysChanged = false;
        }

        popup.Populate(displays);
        popup.ShowNearTray();
    }

    static bool IsStartupEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        return key?.GetValue(RunValue) is string;
    }

    void ToggleStartup()
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        if (IsStartupEnabled())
            key.DeleteValue(RunValue, false);
        else
            key.SetValue(RunValue, $"\"{Application.ExecutablePath}\"");
        startupItem.Checked = IsStartupEnabled();
    }

    static Icon CreateSunIcon()
    {
        using var bmp = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            var color = Color.FromArgb(245, 179, 1);
            using var brush = new SolidBrush(color);
            using var pen = new Pen(color, 3) { StartCap = System.Drawing.Drawing2D.LineCap.Round, EndCap = System.Drawing.Drawing2D.LineCap.Round };
            g.FillEllipse(brush, 9, 9, 14, 14);
            for (int i = 0; i < 8; i++)
            {
                double a = i * Math.PI / 4;
                g.DrawLine(pen,
                    16 + (float)(Math.Cos(a) * 11), 16 + (float)(Math.Sin(a) * 11),
                    16 + (float)(Math.Cos(a) * 14), 16 + (float)(Math.Sin(a) * 14));
            }
        }

        var hIcon = bmp.GetHicon();
        using var temp = Icon.FromHandle(hIcon);
        var icon = (Icon)temp.Clone();
        Native.DestroyIcon(hIcon);
        return icon;
    }

    protected override void ExitThreadCore()
    {
        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        tray.Visible = false;
        tray.Dispose();
        popup.Dispose();
        foreach (var d in displays) d.Dispose();
        base.ExitThreadCore();
    }
}

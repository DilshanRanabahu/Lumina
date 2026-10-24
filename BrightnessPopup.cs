using System.Drawing.Drawing2D;

namespace Lumina;

/// <summary>Dark borderless panel shown above the tray icon with one brightness card per screen.</summary>
sealed class BrightnessPopup : Form
{
    readonly float s;

    public DateTime LastHidden { get; private set; }

    public BrightnessPopup()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        AutoScaleMode = AutoScaleMode.None;
        BackColor = Theme.Background;
        ForeColor = Theme.Text;
        Font = new Font("Segoe UI", 9.5f);
        DoubleBuffered = true;
        s = DeviceDpi / 96f;
    }

    int S(float px) => (int)Math.Round(px * s);

    public void Populate(IReadOnlyList<DisplayItem> displays)
    {
        SuspendLayout();
        foreach (Control c in Controls.Cast<Control>().ToList()) c.Dispose();

        int pad = S(16), width = S(340);
        var header = new Label
        {
            Text = "Brightness",
            Font = new Font("Segoe UI Semibold", 11.5f),
            ForeColor = Theme.Text,
            AutoSize = true,
            Location = new Point(pad, pad),
        };
        Controls.Add(header);

        int y = pad + header.PreferredHeight + S(12);

        if (displays.Count == 0)
        {
            var empty = new Label
            {
                Text = "No screens with brightness control found.",
                ForeColor = Theme.SubText,
                AutoSize = true,
                Location = new Point(pad, y),
            };
            Controls.Add(empty);
            y += empty.PreferredHeight + S(8);
        }

        foreach (var display in displays)
        {
            var card = new DisplayCard(display, s) { Location = new Point(pad, y), Width = width - pad * 2 };
            Controls.Add(card);
            y += card.Height + S(8);
        }

        ClientSize = new Size(width, y + pad - S(8));
        ResumeLayout();
    }

    public void ShowNearTray()
    {
        var area = Screen.FromPoint(Cursor.Position).WorkingArea;
        Location = new Point(area.Right - Width - S(12), area.Bottom - Height - S(12));
        Show();
        Activate();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        // Windows 11 rounded corners and a subtle border; ignored on older Windows.
        int round = Native.DWMWCP_ROUND;
        Native.DwmSetWindowAttribute(Handle, Native.DWMWA_WINDOW_CORNER_PREFERENCE, ref round, sizeof(int));
        int border = ColorTranslator.ToWin32(Theme.Border);
        Native.DwmSetWindowAttribute(Handle, Native.DWMWA_BORDER_COLOR, ref border, sizeof(int));
    }

    protected override void OnDeactivate(EventArgs e)
    {
        base.OnDeactivate(e);
        Hide();
        LastHidden = DateTime.Now;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode == Keys.Escape) Hide();
    }

    protected override bool ProcessDialogKey(Keys keyData)
    {
        if (keyData == Keys.Escape) { Hide(); return true; }
        return base.ProcessDialogKey(keyData);
    }
}

/// <summary>Rounded card showing one screen's icon, name, percentage and slider.</summary>
sealed class DisplayCard : Panel
{
    readonly float s;

    public DisplayCard(DisplayItem display, float scale)
    {
        s = scale;
        DoubleBuffered = true;
        BackColor = Theme.Background;
        Height = S(84);

        int current = display.ReadBrightness();

        var icon = new Label
        {
            Text = display is WmiDisplay ? "" : "",
            Font = Theme.IconFont(14),
            ForeColor = Theme.Accent,
            BackColor = Theme.Card,
            AutoSize = true,
            Location = new Point(S(14), S(13)),
        };
        var name = new Label
        {
            Text = display.Name,
            Font = new Font("Segoe UI Semibold", 9.5f),
            ForeColor = Theme.Text,
            BackColor = Theme.Card,
            AutoSize = true,
            Location = new Point(S(44), S(14)),
        };
        var percent = new Label
        {
            Text = current >= 0 ? $"{current}%" : "n/a",
            ForeColor = Theme.SubText,
            BackColor = Theme.Card,
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleRight,
            Size = new Size(S(56), S(22)),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
        };
        var slider = new ModernSlider
        {
            UiScale = s,
            BackColor = Theme.Card,
            Height = S(26),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            Value = Math.Max(current, 0),
            Enabled = current >= 0,
        };
        slider.ValueChanged += (_, _) =>
        {
            percent.Text = $"{slider.Value}%";
            display.RequestBrightness(slider.Value);
        };

        Controls.AddRange([icon, name, percent, slider]);
        Layout += (_, _) =>
        {
            percent.Location = new Point(Width - percent.Width - S(14), S(12));
            slider.SetBounds(S(10), S(46), Width - S(20), S(26));
        };
    }

    int S(float px) => (int)Math.Round(px * s);

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = Theme.RoundedRect(new RectangleF(0, 0, Width - 1, Height - 1), S(8));
        using var fill = new SolidBrush(Theme.Card);
        using var pen = new Pen(Theme.Border);
        e.Graphics.FillPath(fill, path);
        e.Graphics.DrawPath(pen, path);
    }
}

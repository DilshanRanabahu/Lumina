using System.Drawing.Drawing2D;

namespace Lumina;

static class Theme
{
    public static readonly Color Background = Color.FromArgb(32, 32, 32);
    public static readonly Color Card = Color.FromArgb(43, 43, 43);
    public static readonly Color CardHover = Color.FromArgb(50, 50, 50);
    public static readonly Color Border = Color.FromArgb(58, 58, 58);
    public static readonly Color Text = Color.FromArgb(242, 242, 242);
    public static readonly Color SubText = Color.FromArgb(158, 158, 158);
    public static readonly Color Track = Color.FromArgb(78, 78, 78);
    public static readonly Color Accent = Color.FromArgb(245, 179, 1);

    public static Font IconFont(float size)
    {
        var font = new Font("Segoe Fluent Icons", size);
        if (font.Name == "Segoe Fluent Icons") return font;
        font.Dispose();
        return new Font("Segoe MDL2 Assets", size);
    }

    public static GraphicsPath RoundedRect(RectangleF r, float radius)
    {
        float d = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}

/// <summary>Dark styling for the tray icon's right-click menu.</summary>
sealed class DarkMenuRenderer : ToolStripProfessionalRenderer
{
    public DarkMenuRenderer() : base(new DarkColors()) => RoundedEdges = false;

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        e.TextColor = e.Item.Enabled ? Theme.Text : Theme.SubText;
        base.OnRenderItemText(e);
    }

    protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
    {
        var r = e.ImageRectangle;
        TextRenderer.DrawText(e.Graphics, "", Theme.IconFont(9), r, Theme.Accent,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
    }

    sealed class DarkColors : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground => Theme.Card;
        public override Color ImageMarginGradientBegin => Theme.Card;
        public override Color ImageMarginGradientMiddle => Theme.Card;
        public override Color ImageMarginGradientEnd => Theme.Card;
        public override Color MenuBorder => Theme.Border;
        public override Color MenuItemBorder => Theme.CardHover;
        public override Color MenuItemSelected => Theme.CardHover;
        public override Color CheckBackground => Theme.Card;
        public override Color CheckSelectedBackground => Theme.CardHover;
        public override Color CheckPressedBackground => Theme.CardHover;
        public override Color SeparatorDark => Theme.Border;
        public override Color SeparatorLight => Theme.Border;
    }
}

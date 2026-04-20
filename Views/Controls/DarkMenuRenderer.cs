using System.Drawing;
using System.Windows.Forms;

namespace LocalPlayer.Controls;

public class DarkMenuRenderer : ToolStripProfessionalRenderer
{
    public DarkMenuRenderer() : base(new DarkColorTable())
    {
    }
}

public class DarkColorTable : ProfessionalColorTable
{
    public override Color MenuItemSelected => Color.FromArgb(60, 60, 60);
    public override Color MenuItemBorder => Color.FromArgb(50, 50, 50);
    public override Color MenuBorder => Color.FromArgb(50, 50, 50);
    public override Color ToolStripDropDownBackground => Color.FromArgb(40, 40, 40);
    public override Color ImageMarginGradientBegin => Color.FromArgb(40, 40, 40);
    public override Color ImageMarginGradientMiddle => Color.FromArgb(40, 40, 40);
    public override Color ImageMarginGradientEnd => Color.FromArgb(40, 40, 40);
    public override Color MenuItemPressedGradientBegin => Color.FromArgb(70, 70, 70);
    public override Color MenuItemPressedGradientEnd => Color.FromArgb(70, 70, 70);
    public override Color MenuItemSelectedGradientBegin => Color.FromArgb(60, 60, 60);
    public override Color MenuItemSelectedGradientEnd => Color.FromArgb(60, 60, 60);
    public override Color SeparatorDark => Color.FromArgb(60, 60, 60);
    public override Color SeparatorLight => Color.FromArgb(60, 60, 60);
}
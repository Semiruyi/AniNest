using System.Drawing;

namespace LocalPlayer.Models;

public class DanmakuItem
{
    public string Text { get; set; } = "";
    public double TimeSeconds { get; set; }
    public string ColorHex { get; set; } = "#FFFFFF";
    public int FontSize { get; set; } = 22;
    public float Speed { get; set; } = 120f;

    public Color GetColor() => ColorTranslator.FromHtml(ColorHex);
}

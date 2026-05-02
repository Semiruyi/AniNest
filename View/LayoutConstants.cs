using System.Windows;

namespace LocalPlayer.View;

public static class LayoutConstants
{
    public const double TitleBarHeight = 40;
    public static readonly GridLength TitleBarRowHeight = new(TitleBarHeight);

    public const double TitleBarButtonWidth = 46;
    public const double TitleBarButtonHeight = 40;
    public const double TitleBarAppIconSize = 20;
    public const double TitleBarIconSize = 13;
    public const double TitleBarIconStroke = 1;

    public static readonly Thickness TitleBarLeftMargin = new(12, 0, 0, 0);
}

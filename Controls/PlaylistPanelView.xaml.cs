using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using LocalPlayer.Primitives;
using LocalPlayer.Model;

// 消歧义：UseWindowsForms 隐式导入与 WPF 类型冲突
using Button = System.Windows.Controls.Button;
using Point = System.Windows.Point;

namespace LocalPlayer.Controls;

public partial class PlaylistPanelView : System.Windows.Controls.UserControl
{
    public PlaylistPanelView()
    {
        InitializeComponent();
    }

    public event EventHandler<PlaylistItem>? EpisodeSelected;
    public event EventHandler? MouseEnterBorder;
    public event EventHandler? MouseLeaveBorder;

    public int SelectedIndex
    {
        get => PlaylistBox.SelectedIndex;
        set => PlaylistBox.SelectedIndex = value;
    }

    public PlaylistItem? SelectedItem => PlaylistBox.SelectedItem as PlaylistItem;

    public int ItemCount => PlaylistBox.Items.Count;

    public void SetItems(IEnumerable<PlaylistItem> items)
    {
        PlaylistBox.Items.Clear();
        foreach (var item in items)
            PlaylistBox.Items.Add(item);
        EpisodeCountText.Text = PlaylistBox.Items.Count > 0 ? $"{PlaylistBox.Items.Count} 集" : "";
    }

    public void SetCountText(string text)
    {
        EpisodeCountText.Text = text;
    }

    private void EpisodeButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is PlaylistItem item)
        {
            int index = item.Number - 1;
            if (index >= 0 && index < PlaylistBox.Items.Count)
            {
                if (index == PlaylistBox.SelectedIndex) return;

                var oldItem = PlaylistBox.SelectedItem as PlaylistItem;
                if (oldItem != null)
                    oldItem.IsPlayed = true;

                PlaylistBox.SelectedIndex = index;
            }
        }
    }

    private void PlaylistBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (PlaylistBox.SelectedItem is PlaylistItem item)
            EpisodeSelected?.Invoke(this, item);
    }

    private void RootBorder_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        => MouseEnterBorder?.Invoke(this, EventArgs.Empty);

    private void RootBorder_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        => MouseLeaveBorder?.Invoke(this, EventArgs.Empty);

    // ========== 入场动画 ==========

    public async Task AnimateEpisodeButtonsEntrance()
    {
        await Task.Delay(100);
        if (!IsLoaded) return;

        var buttons = FindVisualChildren<Button>(PlaylistBox);
        for (int i = 0; i < buttons.Count; i++)
        {
            var btn = buttons[i];
            var delayMs = i * 35;
            btn.RenderTransformOrigin = new Point(0.5, 0.5);
            var st = new ScaleTransform(0.88, 0.88);
            btn.RenderTransform = st;
            btn.Opacity = 0;
            st.BeginAnimation(ScaleTransform.ScaleXProperty,
                AnimationHelper.CreateAnim(0.88, 1.0, 420, AnimationHelper.EaseOut, delayMs));
            st.BeginAnimation(ScaleTransform.ScaleYProperty,
                AnimationHelper.CreateAnim(0.88, 1.0, 420, AnimationHelper.EaseOut, delayMs));
            btn.BeginAnimation(UIElement.OpacityProperty,
                AnimationHelper.CreateAnim(0, 1, 320, beginTimeMs: delayMs));
        }
    }

    private static List<T> FindVisualChildren<T>(DependencyObject parent) where T : DependencyObject
    {
        var result = new List<T>();
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T t) result.Add(t);
            result.AddRange(FindVisualChildren<T>(child));
        }
        return result;
    }
}

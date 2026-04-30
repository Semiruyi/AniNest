using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using LocalPlayer.View.Animations;
using LocalPlayer.Model;

namespace LocalPlayer.View.Player;

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
        await StaggeredEntranceAnimator.AnimateAsync(buttons, fromScale: 0.88);
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

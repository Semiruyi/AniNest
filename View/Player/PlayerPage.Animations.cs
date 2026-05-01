using LocalPlayer.View.Animations;

namespace LocalPlayer.View.Player;

public partial class PlayerPage
{
    public Task FadeOutUIAsync(int durationMs = 250)
        => AnimationHelper.FadeOutAsync(PageRoot, durationMs);
}

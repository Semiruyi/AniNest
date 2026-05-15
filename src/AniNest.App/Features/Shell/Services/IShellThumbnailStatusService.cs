using AniNest.Infrastructure.Localization;
using AniNest.Infrastructure.Thumbnails;

namespace AniNest.Features.Shell.Services;

public interface IShellThumbnailStatusService
{
    ShellThumbnailStatusSnapshot GetStatusSnapshot();
    ShellThumbnailPanelSnapshot GetPanelSnapshot(ILocalizationService localization);
}

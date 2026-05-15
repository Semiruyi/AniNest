using AniNest.Infrastructure.Thumbnails;

namespace AniNest.Features.Shell.Services;

public interface IShellThumbnailStatusService
{
    ShellThumbnailStatusSnapshot GetStatusSnapshot();
}

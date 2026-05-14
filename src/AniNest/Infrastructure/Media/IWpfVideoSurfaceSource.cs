using System.ComponentModel;
using System.Windows.Media;

namespace AniNest.Infrastructure.Media;

public interface IWpfVideoSurfaceSource : INotifyPropertyChanged
{
    ImageSource? CurrentFrame { get; }
    void Refresh();
}

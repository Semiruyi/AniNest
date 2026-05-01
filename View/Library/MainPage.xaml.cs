using System.Linq;
using System.Windows;
using System.Windows.Controls;
using LocalPlayer.Model;
using LocalPlayer.ViewModel;

namespace LocalPlayer.View.Library;

public partial class MainPage : System.Windows.Controls.UserControl
{
    private readonly MainPageViewModel _vm;

    public event Action<object, string, string>? FolderSelected;

    public MainPage(MainPageViewModel vm)
    {
        _vm = vm;
        DataContext = _vm;

        InitializeComponent();

        _vm.FolderSelected += (s, path, name) => FolderSelected?.Invoke(s, path, name);

        Loaded += MainPage_Loaded;
    }

    private async void MainPage_Loaded(object sender, RoutedEventArgs e)
    {
        FolderList.ItemsSource = _vm.FolderItems;

        var loadedItems = await System.Threading.Tasks.Task.Run(() => _vm.LoadFoldersData());

        _vm.FolderItems.Clear();
        foreach (var item in loadedItems)
            _vm.FolderItems.Add(item);

        _vm.EnqueueAllFolders(loadedItems);
    }

    private void Card_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is Border border && border.Tag is string path)
            _vm.TrySelectFolder(path, out _);
    }

    private void DeleteBtn_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string path)
        {
            var item = _vm.FolderItems.FirstOrDefault(i => i.Path == path);
            if (item != null)
                _vm.DeleteFolder(item);
        }
        e.Handled = true;
    }
}

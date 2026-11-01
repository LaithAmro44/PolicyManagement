using System.Windows;
using PolicyManagement.Desktop.ViewModels;

namespace PolicyManagement.Desktop;

public partial class MainWindow : Window
{
    private readonly ApiWindowViewModel _viewModel = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _viewModel;
        Closed += (_, _) => _viewModel.Dispose();
    }

    private void OpenLiveEvents_Click(object sender, RoutedEventArgs e)
    {
        var window = new EventsWindow(
            _viewModel.WebSocketUrl,
            _viewModel.TenantId,
            _viewModel.CurrentTenantName)
        {
            Owner = this
        };
        window.Show();
    }
}

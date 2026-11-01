using System.ComponentModel;
using System.Windows;
using PolicyManagement.Desktop.ViewModels;

namespace PolicyManagement.Desktop;

public partial class EventsWindow : Window
{
    private readonly EventsWindowViewModel _viewModel;

    public EventsWindow(string webSocketUrl, Guid tenantId, string tenantName)
    {
        InitializeComponent();
        _viewModel = new EventsWindowViewModel(webSocketUrl, tenantId, tenantName);
        DataContext = _viewModel;
    }

    protected override async void OnClosing(CancelEventArgs e)
    {
        await _viewModel.DisposeAsync();
        base.OnClosing(e);
    }
}

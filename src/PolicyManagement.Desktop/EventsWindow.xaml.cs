using System.ComponentModel;
using System.Windows;
using PolicyManagement.Desktop.ViewModels;

namespace PolicyManagement.Desktop;

public partial class EventsWindow : Window
{
    private readonly EventsWindowViewModel _viewModel;
    private bool _canClose;

    public EventsWindow(string webSocketUrl, Guid tenantId, string tenantName)
    {
        InitializeComponent();
        _viewModel = new EventsWindowViewModel(webSocketUrl, tenantId, tenantName);
        DataContext = _viewModel;
    }

    protected override async void OnClosing(CancelEventArgs e)
    {
        if (_canClose) { base.OnClosing(e); return; }
        e.Cancel = true;
        await _viewModel.DisposeAsync();
        _canClose = true;
        Close();
    }
}

using System.Collections.ObjectModel;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using PolicyManagement.Contracts;

namespace PolicyManagement.Desktop.ViewModels;

public sealed class EventsWindowViewModel : ObservableObject, IAsyncDisposable
{
    private ClientWebSocket? _socket;
    private CancellationTokenSource? _lifetime;
    private Task? _receiveTask;
    private string _connectionState = "Disconnected";
    private Brush _stateBrush = Brushes.SlateGray;

    public EventsWindowViewModel(string webSocketUrl, Guid tenantId, string tenantName)
    {
        WebSocketUrl = webSocketUrl;
        TenantId = tenantId;
        TenantName = tenantName;
        ConnectCommand = new AsyncCommand(ConnectAsync);
        DisconnectCommand = new AsyncCommand(DisconnectAsync);
        ClearCommand = new RelayCommand(Events.Clear);
    }

    public string WebSocketUrl { get; }
    public Guid TenantId { get; }
    public string TenantName { get; }
    public ObservableCollection<EventFeedItem> Events { get; } = [];
    public AsyncCommand ConnectCommand { get; }
    public AsyncCommand DisconnectCommand { get; }
    public RelayCommand ClearCommand { get; }

    public string ConnectionState
    {
        get => _connectionState;
        private set => SetProperty(ref _connectionState, value);
    }

    public Brush StateBrush
    {
        get => _stateBrush;
        private set => SetProperty(ref _stateBrush, value);
    }

    private async Task ConnectAsync()
    {
        await DisconnectAsync();
        SetState("Connecting…", Brushes.DarkOrange);

        try
        {
            _lifetime = new CancellationTokenSource();
            _socket = new ClientWebSocket();
            var separator = WebSocketUrl.Contains('?') ? '&' : '?';
            var endpoint = new Uri($"{WebSocketUrl}{separator}tenantId={TenantId}");
            await _socket.ConnectAsync(endpoint, _lifetime.Token);
            SetState("Connected", Brushes.ForestGreen);
            _receiveTask = ReceiveLoopAsync(_socket, _lifetime.Token);
        }
        catch (Exception ex)
        {
            SetState($"Connection failed: {ex.Message}", Brushes.Firebrick);
            await DisposeSocketAsync();
        }
    }

    private async Task ReceiveLoopAsync(ClientWebSocket socket, CancellationToken cancellationToken)
    {
        var buffer = new byte[4096];
        try
        {
            while (socket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
            {
                using var message = new MemoryStream();
                WebSocketReceiveResult result;
                do
                {
                    result = await socket.ReceiveAsync(buffer, cancellationToken);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await SetStateOnUiAsync("Disconnected by server", Brushes.Firebrick);
                        return;
                    }

                    message.Write(buffer, 0, result.Count);
                } while (!result.EndOfMessage);

                var json = Encoding.UTF8.GetString(message.ToArray());
                var policyEvent = JsonSerializer.Deserialize<PolicyEventMessage>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (policyEvent is not null)
                {
                    await Application.Current.Dispatcher.InvokeAsync(() =>
                        Events.Insert(0, new EventFeedItem(
                            policyEvent.Type,
                            policyEvent.Policy?.Name ?? policyEvent.PolicyId?.ToString() ?? "Connection established",
                            policyEvent.OccurredAtUtc)));
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Expected when the user disconnects.
        }
        catch (Exception ex)
        {
            await SetStateOnUiAsync($"Disconnected: {ex.Message}", Brushes.Firebrick);
        }
        finally
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                await SetStateOnUiAsync("Disconnected", Brushes.SlateGray);
            }
        }
    }

    private async Task DisconnectAsync()
    {
        if (_socket is null && _lifetime is null)
        {
            SetState("Disconnected", Brushes.SlateGray);
            return;
        }

        _lifetime?.Cancel();
        if (_socket?.State == WebSocketState.Open)
        {
            try
            {
                await _socket.CloseOutputAsync(
                    WebSocketCloseStatus.NormalClosure,
                    "User disconnected",
                    CancellationToken.None);
            }
            catch (WebSocketException)
            {
                // The peer may already have gone away.
            }
        }

        if (_receiveTask is not null)
        {
            try
            {
                await _receiveTask;
            }
            catch (OperationCanceledException)
            {
                // Expected.
            }
        }

        await DisposeSocketAsync();
        SetState("Disconnected", Brushes.SlateGray);
    }

    private ValueTask SetStateOnUiAsync(string state, Brush brush) =>
        new(Application.Current.Dispatcher.InvokeAsync(() => SetState(state, brush)).Task);

    private void SetState(string state, Brush brush)
    {
        ConnectionState = state;
        StateBrush = brush;
    }

    private ValueTask DisposeSocketAsync()
    {
        _socket?.Dispose();
        _socket = null;
        _lifetime?.Dispose();
        _lifetime = null;
        _receiveTask = null;
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync() => await DisconnectAsync();
}

public sealed record EventFeedItem(string Type, string Summary, DateTime OccurredAtUtc);

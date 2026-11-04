using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using PolicyManagement.Contracts;

namespace PolicyManagement.Admin.Infrastructure;

public sealed class PolicySocketHub
{
    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<Guid, ClientConnection>> _connections = new();

    public Guid Add(Guid tenantId, WebSocket socket)
    {
        var connectionId = Guid.NewGuid();
        _connections.GetOrAdd(tenantId, _ => new())[connectionId] = new ClientConnection(socket);
        return connectionId;
    }

    public void Remove(Guid tenantId, Guid connectionId)
    {
        if (_connections.TryGetValue(tenantId, out var tenantConnections))
        {
            tenantConnections.TryRemove(connectionId, out _);
        }
    }

    public async Task BroadcastAsync(PolicyEventMessage message, CancellationToken cancellationToken = default)
    {
        if (!_connections.TryGetValue(message.TenantId, out var tenantConnections))
        {
            return;
        }

        var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        var sends = tenantConnections.Select(async pair =>
        {
            try
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(TimeSpan.FromSeconds(2));
                await pair.Value.SendLock.WaitAsync(timeout.Token);
                try
                {
                    if (pair.Value.Socket.State == WebSocketState.Open)
                        await pair.Value.Socket.SendAsync(bytes, WebSocketMessageType.Text, true, timeout.Token);
                }
                finally { pair.Value.SendLock.Release(); }
            }
            catch (Exception ex) when (ex is WebSocketException or OperationCanceledException or ObjectDisposedException)
            {
                Remove(message.TenantId, pair.Key);
                pair.Value.Socket.Abort();
            }
        });

        await Task.WhenAll(sends);
    }

    private sealed class ClientConnection(WebSocket socket)
    {
        public WebSocket Socket { get; } = socket;
        public SemaphoreSlim SendLock { get; } = new(1, 1);
    }
}

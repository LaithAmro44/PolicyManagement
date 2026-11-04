using System.Net.WebSockets;
using PolicyManagement.Admin.Infrastructure;
using PolicyManagement.Contracts;
using PolicyManagement.Data;

namespace PolicyManagement.Tests;

public sealed class PolicySocketHubTests
{
    [Fact]
    public async Task Broadcast_reaches_only_its_tenant_and_serializes_concurrent_sends()
    {
        var hub = new PolicySocketHub();
        using var bankA = new RecordingSocket();
        using var bankB = new RecordingSocket();
        hub.Add(SeedTenantIds.BankA, bankA);
        hub.Add(SeedTenantIds.BankB, bankB);
        await Task.WhenAll(Enumerable.Range(0, 10).Select(_ =>
            hub.BroadcastAsync(new PolicyEventMessage(Guid.NewGuid(), "PolicyUpdated",
                SeedTenantIds.BankA, null, null, DateTime.UtcNow))));
        Assert.Equal(10, bankA.Sent);
        Assert.Equal(0, bankB.Sent);
        Assert.Equal(1, bankA.MaximumConcurrentSends);
    }

    [Fact]
    public async Task Failed_socket_does_not_stop_healthy_clients()
    {
        var hub = new PolicySocketHub();
        using var failed = new RecordingSocket { Fail = true };
        using var healthy = new RecordingSocket();
        hub.Add(SeedTenantIds.BankA, failed);
        hub.Add(SeedTenantIds.BankA, healthy);
        await hub.BroadcastAsync(new PolicyEventMessage(Guid.NewGuid(), "PolicyCreated",
            SeedTenantIds.BankA, null, null, DateTime.UtcNow));
        Assert.Equal(1, healthy.Sent);
        Assert.Equal(WebSocketState.Aborted, failed.State);
    }

    private sealed class RecordingSocket : WebSocket
    {
        private int _activeSends;
        private WebSocketState _state = WebSocketState.Open;
        public int Sent { get; private set; }
        public int MaximumConcurrentSends { get; private set; }
        public bool Fail { get; init; }
        public override WebSocketState State => _state;
        public override WebSocketCloseStatus? CloseStatus => null;
        public override string? CloseStatusDescription => null;
        public override string? SubProtocol => null;
        public override void Abort() => _state = WebSocketState.Aborted;
        public override void Dispose() { }
        public override Task CloseAsync(WebSocketCloseStatus status, string? description, CancellationToken token) => Task.CompletedTask;
        public override Task CloseOutputAsync(WebSocketCloseStatus status, string? description, CancellationToken token) => Task.CompletedTask;
        public override Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken token) => throw new NotSupportedException();
        public override async Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType type, bool end, CancellationToken token)
        {
            if (Fail) throw new WebSocketException();
            var active = Interlocked.Increment(ref _activeSends);
            MaximumConcurrentSends = Math.Max(MaximumConcurrentSends, active);
            await Task.Delay(5, token);
            Sent++;
            Interlocked.Decrement(ref _activeSends);
        }
    }
}

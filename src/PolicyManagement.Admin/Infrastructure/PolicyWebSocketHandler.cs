using System.Net.WebSockets;
using Microsoft.EntityFrameworkCore;
using PolicyManagement.Data;

namespace PolicyManagement.Admin.Infrastructure;

public sealed class PolicyWebSocketHandler(
    PolicySocketHub hub,
    TenantContext tenantContext,
    PolicyDbContext db)
{
    public async Task HandleAsync(HttpContext context)
    {
        if (!context.WebSockets.IsWebSocketRequest ||
            !Guid.TryParse(context.Request.Query["tenantId"], out var tenantId) ||
            !await db.Tenants.AsNoTracking().AnyAsync(x => x.Id == tenantId, context.RequestAborted))
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        tenantContext.SetTenant(tenantId);
        using var socket = await context.WebSockets.AcceptWebSocketAsync();
        var connectionId = hub.Add(tenantId, socket);

        try
        {
            var buffer = new byte[1024];
            while (socket.State == WebSocketState.Open)
            {
                var result = await socket.ReceiveAsync(buffer, context.RequestAborted);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // The request or application stopped.
        }
        catch (WebSocketException)
        {
            // The client disconnected without a close handshake.
        }
        finally
        {
            hub.Remove(tenantId, connectionId);
            if (socket.State == WebSocketState.CloseReceived)
            {
                try
                {
                    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                    await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "Disconnected", timeout.Token);
                }
                catch (Exception ex) when (ex is WebSocketException or OperationCanceledException)
                {
                    // The peer may have already closed the transport.
                }
            }
        }
    }
}

using System.Net;
using System.Net.WebSockets;
using System.Text;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://localhost:8085");
var app = builder.Build();
List<WebSocket> connections = [];
app.UseWebSockets();
app.Map("/ws", async context => 
{
    if (context.WebSockets.IsWebSocketRequest) {
        var clientName = context.Request.Query["name"];
        using var ws = await context.WebSockets.AcceptWebSocketAsync();
        connections.Add(ws);
        await Broadcast($"{clientName} just joined");

        await Receive(ws,
            async (result, buffer) =>
            {
                if (result.MessageType == WebSocketMessageType.Text)
                {
                    string message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    await Broadcast(DateTime.Now.ToString("HH:MM:ss") + " " + clientName + ": " + message);
                } else if (result.MessageType == WebSocketMessageType.Close 
                    || ws.State == WebSocketState.Closed 
                    || ws.State == WebSocketState.Aborted) 
                {
                    connections.Remove(ws);
                    await Broadcast($"{clientName} has left the chat");
                    if (result.CloseStatus != null)
                        await ws.CloseAsync(result.CloseStatus.Value, result.CloseStatusDescription, CancellationToken.None);
                    else await ws.CloseAsync(WebSocketCloseStatus.Empty, result.CloseStatusDescription, CancellationToken.None);
                }
            }
        ); 
    } else 
    { 
        context.Response.StatusCode = (int) HttpStatusCode.BadRequest; 
    } 
});


async Task Receive(WebSocket socket, Action<WebSocketReceiveResult, byte[]> handleMessage)
{
    var buffer = new byte[1024 * 4];
    while (socket.State == WebSocketState.Open)
    {
        var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
        handleMessage(result, buffer);
    }
}

async Task Broadcast(string message)
{
    byte[] bytes = Encoding.UTF8.GetBytes(message);
    foreach (var ws in connections)
    {
        if (ws.State == System.Net.WebSockets.WebSocketState.Open)
        {
            ArraySegment<byte> arraySegment = new(bytes, 0, bytes.Length);
            await ws.SendAsync(arraySegment, WebSocketMessageType.Text, true, CancellationToken.None);
        }
    }
}

await app.RunAsync();


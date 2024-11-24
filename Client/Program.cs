using System.Net.WebSockets;
using System.Text;

string name;
while (true)
{
    System.Console.WriteLine("Input name: ");
    string? response = Console.ReadLine();
    if (response != null)
    {
        name = response;
        break;
    }
}

ClientWebSocket ws = new();

await ws.ConnectAsync(new Uri($"ws://localhost:8085/ws?name={name}"), CancellationToken.None);

Task receive = Task.Run(async () =>
{
    byte[] buffer = new byte[1024];
    while (true) {
        var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

        if (result.MessageType == WebSocketMessageType.Close)
        {
            break;
        }

        var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
        System.Console.WriteLine(message);
    }
});

Task send = Task.Run(async () => 
{
    while (true)
    {
        var message = Console.ReadLine();
        if (message == "~exit")
        {
            break;
        }
        var bytes = Encoding.UTF8.GetBytes(message);
        await ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
    }
});

await Task.WhenAny(send, receive);

if (ws.State != WebSocketState.Closed)
{
    await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
}

await Task.WhenAll(send, receive);
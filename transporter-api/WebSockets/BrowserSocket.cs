using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using transporter_api.Extensions;

namespace transporter_api.WebSockets
{
    public static class BrowserSocket
    {
        public static async Task<bool> TryConnect(HttpContext context)
        {
            if (context.WebSockets.IsWebSocketRequest)
            {
                WebSocket webSocket =
                    await context.WebSockets.AcceptWebSocketAsync();

                await Connect(context, webSocket);
                return true;
            }
            //var s = "driver_id is invalid";
            //byte[] data = Encoding.UTF8.GetBytes(s);
            //await context.Response.Body.WriteAsync(data, 0, data.Length);
            return false;
        }

        public static async Task Connect(HttpContext context, WebSocket webSocket)
        {
            var tokenSource2 = new CancellationTokenSource();
            CancellationToken ct = tokenSource2.Token;

            var task = Task.Run(() => Run(webSocket), 
                tokenSource2.Token);

            var buffer = new byte[1024 * 4];
            var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer),
                CancellationToken.None);

            while (!result.CloseStatus.HasValue)
            {
                result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer),
                    CancellationToken.None);
            }

            tokenSource2.Cancel();
            await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, 
            "end", CancellationToken.None);
        }

        public async static Task Run(WebSocket webSocket)
        {
            while (true)
            {
                await webSocket.SendAsync(new MapSocketMessage
                {
                    Payload = MobileSocket.Drivers.Values.ToArray()
                });
                Thread.Sleep(5000);
                Console.WriteLine("Send drivers to browser");
            }
        }
    }

    public class MapSocketMessage
    {
        public string Operation = SocketOperation.Browser.Map;
        public VehicleOnMap[] Payload { get; set; }
    }

    public class VehicleOnMap
    {
        public string Id { get; set; }
        public GeoPoint Position { get; set; }
        public string Badge { get; set; }
    }
}

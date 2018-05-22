using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Concurrent;
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
        // key driverId
        public static List<WebSocket> BrowserWebSockets
            = new List<WebSocket>();

        public static async Task<bool> TryConnect(HttpContext context)
        {
            if (context.WebSockets.IsWebSocketRequest)
            {
                WebSocket webSocket =
                    await context.WebSockets.AcceptWebSocketAsync();

                BrowserWebSockets.Add(webSocket);


                await WrapConnection(context, webSocket);
                return true;
            }
            return false;
        }

        public static async Task WrapConnection(HttpContext context, WebSocket webSocket)
        {
            try
            {
                await Connect(context, webSocket);
            }
            catch (WebSocketException)
            {
                if (!BrowserWebSockets.Remove(webSocket))
                    Console.WriteLine($"cant remove from sockets after ws exception");
                throw;
            }
        }

        public static async Task Connect(HttpContext context, WebSocket webSocket)
        {
            var buffer = new byte[1024 * 4];
            var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer),
                CancellationToken.None);

            while (!result.CloseStatus.HasValue)
            {
                result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer),
                    CancellationToken.None);
            }

            await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, 
            "end", CancellationToken.None);
        }

        public async static Task SendToAllAsync(object obj)
        {
            await Task.Run(async () =>
            {
                foreach (var ws in BrowserWebSockets)
                {
                    try
                    {
                        await ws.SendAsync(obj);
                    }
                    catch (WebSocketException ex)
                    {
                        Console.WriteLine("browser socket sending exception: " + ex.ToString());
                        BrowserWebSockets.Remove(ws);
                    }
                }
            });
        }
    }

    public class OrdersSocketMessage
    {
        public string Operation = SocketOperation.Browser.Orders;
        public List<Order> Payload { get; set; }
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

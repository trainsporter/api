using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace transporter_api.WebSockets
{
    public class GeoPoint
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
    }

    public class OrderAvailablePayload
    {
        public string Operation { get; set; } = "order_available";
        public Order Payload { get; set; }
    }

    public class Order
    {
        public string Id { get; set; }
        public GeoPoint Pickup { get; set; }
        public GeoPoint Dropoff { get; set; }
        public string Status { get; set; }
    }

    public class OrderStatus
    {
        public const string Unnassigned = "unnassigned";
        public const string Assigned = "assigned";
        public const string Serving = "serving";
        public const string Done = "done";
        public const string Cancelled = "cancelled";
    }


    public static class MobileSocket
    {
        public static List<Order> Orders = new List<Order>
        {
            new Order
            {
                Id = "1",
                Pickup = new GeoPoint{Latitude = 55.785681, Longitude = 49.235803},
                Dropoff = new GeoPoint{Latitude = 55.830431, Longitude = 49.066081},
                Status = OrderStatus.Unnassigned
            },
            new Order
            {
                Id = "2",
                Pickup = new GeoPoint{Latitude = 55.823864, Longitude = 49.127644},
                Dropoff = new GeoPoint{Latitude = 55.788192, Longitude = 49.121085},
                Status = OrderStatus.Unnassigned
            }
        };

        public static Dictionary<int, int> Drivers;
        public static ConcurrentDictionary<int, WebSocket> MobileWebSockets
            = new ConcurrentDictionary<int, WebSocket>();

        public static bool SendIsRunned = false;
        public static bool WsActive = false;

        public class MobileSocketMessage
        {
            public string Operation { get; set; }
            public object Payload { get; set; } 
        }

        public static class Operation
        {
            public const string Position = "position";
        }

        public class Position
        {
            public double Latitude { get; set; }
            public double Longitude { get; set; }
        }

        public static async Task<bool> TryConnect(HttpContext context)
        {
            if (context.WebSockets.IsWebSocketRequest)
            {
                var queryDict = QueryHelpers.ParseQuery(context.Request.QueryString.ToString());
                if (queryDict.TryGetValue("driver_id", out var driverIdString))
                {
                    if (int.TryParse(driverIdString.ToString(), out int driverId))
                    {
                        WebSocket webSocket = 
                            await context.WebSockets.AcceptWebSocketAsync();

                        MobileWebSockets.AddOrUpdate(driverId, webSocket, (key, oldWs) => webSocket);

                        await Connect(context, webSocket, driverId);
                        return true;
                    }
                }
            }
            //var s = "driver_id is invalid";
            //byte[] data = Encoding.UTF8.GetBytes(s);
            //await context.Response.Body.WriteAsync(data, 0, data.Length);
            return false;
        }

        public static async Task Connect(HttpContext context, WebSocket webSocket, int driverId)
        {
            var buffer = new byte[1024 * 4];
            WebSocketReceiveResult result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer),
                CancellationToken.None);
            while (!result.CloseStatus.HasValue)
            {
                var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                string answerMessage = ParseMobileSocketMessage(message);

                if (message != null)
                    await SendAsync(webSocket, answerMessage + $" --driverId: {driverId}, " +
                        $"opened mobile sockets: {MobileWebSockets.Count}");

                result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer),
                    CancellationToken.None);
            }
            await webSocket.CloseAsync(result.CloseStatus.Value, result.CloseStatusDescription,
                CancellationToken.None);
            MobileWebSockets.TryRemove(driverId, out var removedWebSocket);
        }

        public static string ParseMobileSocketMessage(string message)
        {
            var mobileSocketMessage = JsonConvert.DeserializeObject<MobileSocketMessage>(message);

            JToken token = JObject.Parse(message);

            var operationType = (string)token.SelectToken("operation");

            if (operationType == Operation.Position)
            {
                var position = token.SelectToken("payload").ToObject<Position>();
                //JsonConvert.DeserializeObject<Position>(mobileSocketMessage.Payload);
                //var position = (Position)Convert.ChangeType(mobileSocketMessage.Payload,
                //    typeof(Position));


                return
                    $"Hi! Got it, your position: {position.Latitude}, {position.Longitude}";
                //await SendAsync(webSocket, $"payload: {mobileSocketMessage.Payload}");
            }
            else
            {
                return "=(";
            }
        }

        public static async Task SendAsync(WebSocket webSocket, string message)
        {
            var arr = Encoding.UTF8.GetBytes(message);

            var sendBuffer = new ArraySegment<byte>(
                    array: arr,
                    offset: 0,
                    count: arr.Length);

            await webSocket.SendAsync(sendBuffer,
                WebSocketMessageType.Text, true, CancellationToken.None);
        }

        public static async Task StartSendOrders()
        {
            int i = 0;
            Order rdmNewOrder;
            while (true && WsActive)
            {
                i++;
                rdmNewOrder = new Order
                {
                    Id = i.ToString(),
                    Pickup = new GeoPoint { Latitude = Random.Next() + Random.NextDouble(), Longitude = Random.Next() + Random.NextDouble() },
                    Dropoff = new GeoPoint { Latitude = Random.Next() + Random.NextDouble(), Longitude = Random.Next() + Random.NextDouble() },
                    Status = OrderStatus.Unnassigned
                };

                await SendToAllMobileSockets(new OrderAvailablePayload
                {
                    Payload = rdmNewOrder
                });

                Thread.Sleep(6000);
            }
        }

        public static async Task SendToAllMobileSockets(object obj)
        {
            foreach (var mobileWs in MobileWebSockets)
            {
                await SendAsync(mobileWs.Value, JsonConvert.SerializeObject(obj,
                        new JsonSerializerSettings
                        {
                            ContractResolver = new CamelCasePropertyNamesContractResolver()
                        }));
            }
        }

        public static async Task SendToAllMobileSockets(string message)
        {
            List<int> disposedWebSocketsKeys = new List<int>();
            foreach (var mobileWs in MobileWebSockets)
            {
                try
                {
                    await SendAsync(mobileWs.Value, message);
                }
                catch (ObjectDisposedException)
                {
                    disposedWebSocketsKeys.Add(mobileWs.Key);
                }
            }

            foreach (var disposedWsKey in disposedWebSocketsKeys)
            {
                MobileWebSockets.TryRemove(disposedWsKey, out var disposedWs);
            }
        }

        public static Random Random = new Random();
    }
}

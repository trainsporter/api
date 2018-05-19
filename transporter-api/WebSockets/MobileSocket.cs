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
using transporter_api.Extensions;

namespace transporter_api.WebSockets
{
    public static class SocketOperation
    {
        public static class Mobile
        {
            public const string OrderAvailable = "order_available";
            public const string Position = "position";
        }
        public static class Browser
        {
            public const string Map = "map";
        }
    }

    public class GeoPoint
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
    }

    public class OrderAvailablePayload
    {
        public string Operation = SocketOperation.Mobile.OrderAvailable;
        public Order Payload { get; set; }
    }

    public class Order
    {
        public string Id { get; set; }
        public string Driver_id { get; set; }
        public GeoPoint Pickup { get; set; }
        public GeoPoint Dropoff { get; set; }
        public OrderStatus Status { get; set; }
    }

    public enum OrderStatus
    {
        unassigned,
        assigned,
        serving,
        done,
        cancelled
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
                Status = OrderStatus.unassigned
            },
            new Order
            {
                Id = "2",
                Pickup = new GeoPoint{Latitude = 55.823864, Longitude = 49.127644},
                Dropoff = new GeoPoint{Latitude = 55.788192, Longitude = 49.121085},
                Status = OrderStatus.unassigned
            }
        };

        // key driverId
        public static ConcurrentDictionary<string, VehicleOnMap> Drivers
            = new ConcurrentDictionary<string, VehicleOnMap>();

        // key driverId
        public static ConcurrentDictionary<string, WebSocket> MobileWebSockets
            = new ConcurrentDictionary<string, WebSocket>();

        public static bool SendIsRunned = false;
        public static bool WsActive = false;

        public class MobileSocketMessage
        {
            public string Operation { get; set; }
            public object Payload { get; set; } 
        }

        public static async Task<bool> TryConnect(HttpContext context)
        {
            if (context.WebSockets.IsWebSocketRequest)
            {
                var queryDict = QueryHelpers.ParseQuery(context.Request.QueryString.ToString());
                if (queryDict.TryGetValue("driver_id", out var driverIdString))
                {
                    var driverId = driverIdString.ToString();

                    if (MobileWebSockets.TryGetValue(driverId, out WebSocket oldWebSocket))
                    {
                        await oldWebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, 
                            $"new socket opened by driver_id = \"{driverId}\"",
                            CancellationToken.None);
                    }

                    WebSocket webSocket =
                        await context.WebSockets.AcceptWebSocketAsync();

                    if (!MobileWebSockets.TryAdd(driverId, webSocket))
                        Console.WriteLine($"socket already exist for driver_id = \"{driverId}\"");

                    await ConnectRec(context, webSocket, driverId);
                    return true;
                }
            }
            //var s = "driver_id is invalid";
            //byte[] data = Encoding.UTF8.GetBytes(s);
            //await context.Response.Body.WriteAsync(data, 0, data.Length);
            return false;

        }

        public static async Task ConnectRec(HttpContext context, WebSocket webSocket,
            string driverId)
        {
            try
            {
                await Connect(context, webSocket, driverId);
            }
            catch (WebSocketException ex)
            {
                if (!Drivers.TryRemove(driverId, out var removedVehicleOnMap))
                    Console.WriteLine($"cant remove from drivers after ws exception");
                if (!MobileWebSockets.TryRemove(driverId, out var removedWebSocket))
                    Console.WriteLine($"cant remove from sockets after ws exception");
                throw;
            }
        }

        public static async Task Connect(HttpContext context, WebSocket webSocket, 
            string driverId)
        {
            var buffer = new byte[1024 * 4];
            WebSocketReceiveResult result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer),
                CancellationToken.None);
            while (!result.CloseStatus.HasValue)
            {
                var message = Encoding.UTF8.GetString(buffer, 0, result.Count);

                if (TryParseMobilePosition(message, out GeoPoint position))
                {
                    var vehOnMap = new VehicleOnMap
                    {
                        Id = driverId,
                        Position = position,
                        Badge = ""
                    };
                    Drivers.AddOrUpdate(driverId, vehOnMap, (key, oldPosition) => vehOnMap);
                    await webSocket.SendAsync($"position saved, --driverId: {driverId}, " +
                        $"opened mobile sockets: {MobileWebSockets.Count}, " +
                        $"drivers positions count: {Drivers.Count}");
                }
                else
                {
                    await webSocket.SendAsync($"ERROR position not saved --driverId: {driverId}, " +
                        $"opened mobile sockets: {MobileWebSockets.Count}" +
                        $"drivers positions count: {Drivers.Count}");
                }

                result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer),
                    CancellationToken.None);
            }

            if (!Drivers.TryRemove(driverId, out var removedVehicleOnMap))
                Console.WriteLine($"cant remove from drivers after ws closing, close status: \"{result.CloseStatus.Value}\"");
            if (!MobileWebSockets.TryRemove(driverId, out var removedWebSocket))
                Console.WriteLine($"cant remove from sockets after ws closing, close status: \"{result.CloseStatus.Value}\"");

            await webSocket.CloseAsync(result.CloseStatus.Value, result.CloseStatusDescription,
                CancellationToken.None);
        }

        public static bool TryParseMobilePosition(string message, out GeoPoint position)
        {
            position = null;
            var mobileSocketMessage = JsonConvert.DeserializeObject<MobileSocketMessage>(message);

            JToken token = JObject.Parse(message);

            var operationType = (string)token.SelectToken("operation");

            if (operationType == SocketOperation.Mobile.Position)
                position = token.SelectToken("payload").ToObject<GeoPoint>();

            return position != null;
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
                    Status = OrderStatus.unassigned
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
                List<string> disposedWebSocketsKeys = new List<string>();

                try
                {
                    await mobileWs.Value.SendAsync(JsonConvert.SerializeObject(obj,
                        new JsonSerializerSettings
                        {
                            ContractResolver = new CamelCasePropertyNamesContractResolver(),
                            Converters = new List<JsonConverter>()
                            {
                                new Newtonsoft.Json.Converters.StringEnumConverter()
                            }
                        }));
                }
                catch (WebSocketException)
                {
                    disposedWebSocketsKeys.Add(mobileWs.Key);
                }

                foreach (var disposedWsKey in disposedWebSocketsKeys)
                {
                    MobileWebSockets.TryRemove(disposedWsKey, out var disposedWs);
                }
            }
        }

        public static Random Random = new Random();
    }
}

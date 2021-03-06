﻿using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
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
            public const string Orders = "orders";
        }
    }

    public class GLocation
    {
        public string Address { get; set; }
        public GeoPoint Position { get; set; }
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
        public GLocation Pickup { get; set; }
        public GLocation Dropoff { get; set; }
        public OrderStatus Status { get; set; }
    }

    public class NewOrderDto
    {
        [Required(AllowEmptyStrings = false)]
        public string PickUp { get; set; }
        [Required(AllowEmptyStrings = false)]
        public string DropOff { get; set; }
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

                    MobileWebSockets.AddOrUpdate(driverId, webSocket, 
                        (key, oldWs) => webSocket);

                    await WrapConnection(context, webSocket, driverId);
                    return true;
                }
            }
            return false;
        }

        public static async Task WrapConnection(HttpContext context, WebSocket webSocket,
            string driverId)
        {
            try
            {
                await Connect(context, webSocket, driverId);
            }
            catch (WebSocketException)
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
                    BrowserSocket.SendToAllAsync(new MapSocketMessage
                    {
                        Payload = Drivers.Values.ToArray()
                    });
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
            // 

            position = null;
            var mobileSocketMessage = JsonConvert.DeserializeObject<MobileSocketMessage>(message);

            JToken token = JObject.Parse(message);

            var operationType = (string)token.SelectToken("operation");

            if (operationType == SocketOperation.Mobile.Position)
                position = token.SelectToken("payload").ToObject<GeoPoint>();

            return position != null;
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

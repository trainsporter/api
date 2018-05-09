﻿using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
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
        public decimal Latitude { get; set; }
        public decimal Longitude { get; set; }
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
        public static List<Order> Orders;
        public static Dictionary<int, int> Drivers;
        public static Dictionary<int, WebSocket> MobileWebSockets;

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
            public decimal Latitude { get; set; }
            public decimal Longitude { get; set; }
        }

        public static async Task TryConnect(HttpContext context)
        {

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
                    await SendAsync(webSocket, answerMessage + $" --driverId: {driverId}");

                result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer),
                    CancellationToken.None);
            }
            await webSocket.CloseAsync(result.CloseStatus.Value, result.CloseStatusDescription,
                CancellationToken.None);
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
            while (true)
            {
                Thread.Sleep(1000);

                //Orders.
            }
        }
    }
}

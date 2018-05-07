﻿using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace transporter_api.WebSockets
{
    public static class MobileSocket
    {
        public static Dictionary<int, int> Drivers;

        public class MobileSocketMessage
        {
            public string Operation { get; set; }
            public dynamic Payload { get; set; } 
        }

        public static class Operation
        {
            public const string Position = "position";
        }

        public class Position
        {
            public long Latitude { get; set; }
            public long Longitude { get; set; }
        }

        public static async Task Connect(HttpContext context, WebSocket webSocket)
        {
            var buffer = new byte[1024 * 4];
            WebSocketReceiveResult result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer),
                CancellationToken.None);
            while (!result.CloseStatus.HasValue)
            {
                var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                var mobileSocketMessage = JsonConvert.DeserializeObject<MobileSocketMessage>(message);
                if (mobileSocketMessage.Operation == Operation.Position)
                {
                    var position = (Position)Convert.ChangeType(mobileSocketMessage.Payload,
                        typeof(Position));

                    await SendAsync(webSocket,
                        $"Hi! Got it, your position: {position.Latitude}, {position.Longitude}");
                    //await SendAsync(webSocket, $"payload: {mobileSocketMessage.Payload}");
                }
                else
                {
                    await SendAsync(webSocket,
                        $"Dude, I don't know '{mobileSocketMessage.Operation}' operation.");
                }
                //await SendAsync(webSocket, $"hey, {mobileSocketMessage.Operation}");
                //await webSocket.SendAsync(new ArraySegment<byte>(buffer, 0, result.Count),
                //    result.MessageType, result.EndOfMessage, CancellationToken.None);

                result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer),
                    CancellationToken.None);
            }
            await webSocket.CloseAsync(result.CloseStatus.Value, result.CloseStatusDescription,
                CancellationToken.None);
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
    }
}

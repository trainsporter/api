using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace transporter_api.Extensions
{
    public static class WebSocketExtension
    {
        public static async Task SendAsync(this WebSocket webSocket, string message)
        {
            var arr = Encoding.UTF8.GetBytes(message);

            var sendBuffer = new ArraySegment<byte>(
                    array: arr,
                    offset: 0,
                    count: arr.Length);

            await webSocket.SendAsync(sendBuffer,
                WebSocketMessageType.Text, true, CancellationToken.None);
        }

        public static async Task SendAsync(this WebSocket webSocket, object obj)
        {
            string json = JsonConvert.SerializeObject(obj,
                        new JsonSerializerSettings
                        {
                            ContractResolver = new CamelCasePropertyNamesContractResolver(),
                            Converters = new List<JsonConverter>()
                            {
                                new Newtonsoft.Json.Converters.StringEnumConverter()
                            }
                        });
            var arr = Encoding.UTF8.GetBytes(json);

            var sendBuffer = new ArraySegment<byte>(
                    array: arr,
                    offset: 0,
                    count: arr.Length);

            await webSocket.SendAsync(sendBuffer,
                WebSocketMessageType.Text, true, CancellationToken.None);
        }
    }
}

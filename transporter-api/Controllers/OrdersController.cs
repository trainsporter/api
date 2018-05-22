using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore.Internal;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using transporter_api.Extensions;
using transporter_api.Models.Dtos;
using transporter_api.WebSockets;

namespace transporter_api.Controllers
{
    [Route("orders")]
    public class OrdersController : Controller
    {

        public static string GeocodingApiKey =
            "AIzaSyDzJOAAULXGQg4syXYuH04XP7tNkfOj_Uw";
       

        public static List<Order> Orders = new List<Order>();

        private static int _orderId = 0;
        // GET api/orders
        [HttpGet]
        public IEnumerable<string> Get()
        {
            return new string[] { "value1", "value2" };
        }

        // GET api/orders/5
        [HttpGet("{id}")]
        public string Get(int id)
        {
            return "value";
        }

        // POST api/orders
        [HttpPost]
        public async Task<IActionResult> Post([FromBody]NewOrderDto newOrderDto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            _orderId++;

            var pickupPosition = await GetByAddress(newOrderDto.PickUp);
            if (pickupPosition == null)
                return BadRequest($"address = \"{newOrderDto.PickUp}\"  not found");

            var dropoffPosition = await GetByAddress(newOrderDto.DropOff);
            if (dropoffPosition == null)
                return BadRequest($"address = \"{newOrderDto.DropOff}\" address not found");

            var newOrder = new Order
            {
                Id = _orderId.ToString(),
                Status = OrderStatus.unassigned,
                Pickup = new GLocation
                {
                    Address = newOrderDto.PickUp,
                    Position = pickupPosition,
                },
                Dropoff = new GLocation
                {
                    Address = newOrderDto.DropOff,
                    Position = dropoffPosition,
                }
            };
            Orders.Add(newOrder);

            BrowserSocket.SendToAllAsync(new OrdersSocketMessage
            {
                Payload = Orders
            });

            MobileSocket.SendToAllMobileSockets(new OrderAvailablePayload
            {
                Payload = newOrder
            });

            return Ok(newOrder);
        }

        private async Task<GeoPoint> GetByAddress(string address)
        {
            using (var client = new HttpClient())
            {
                client.BaseAddress = new Uri("https://maps.googleapis.com");
                var response = await client
                    .GetAsync($"/maps/api/geocode/json?address={address}&key={GeocodingApiKey}");
                response.EnsureSuccessStatusCode();

                var stringResult = await response.Content.ReadAsStringAsync();
                var position = JsonConvert.DeserializeObject<GeocodingModel>(stringResult);

                var geoLocation = position.results.FirstOrDefault();
                return (geoLocation != null)
                    ? new GeoPoint
                    {
                        Latitude = geoLocation.geometry.location.lat,
                        Longitude = geoLocation.geometry.location.lng,
                    }
                    : null;
            }
        }

        // PUT api/orders/5
        [HttpPut("{id}")]
        public void Put(int id, [FromBody]string value)
        {

        }

        public static Dictionary<OrderStatus, StatusRule> StatusRules =
            new Dictionary<OrderStatus, StatusRule>
            {
                { OrderStatus.unassigned, new StatusRule(false, OrderStatus.assigned) },
                { OrderStatus.assigned, new StatusRule(true, OrderStatus.serving, OrderStatus.unassigned) },
                { OrderStatus.serving, new StatusRule(true, OrderStatus.done) },
                { OrderStatus.done, null },
                { OrderStatus.cancelled, null },
            };


        public class StatusRule
        {
            public OrderStatus[] Next;
            public bool IsOwned;

            public StatusRule(bool isOwned, params OrderStatus[] nextStatuses)
            {
                Next = nextStatuses;
                IsOwned = isOwned;
            }

            public string ShowNext()
            {
                return Next.Select(x => x.ToString()).Join(",");
            }
        }

        [HttpPut("{id}/status")]
        public async Task<IActionResult> Put(int id, [FromBody]Order updOrder)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            OrderStatus newStatus = updOrder.Status;
            var order = Orders.SingleOrDefault(o => o.Id == id.ToString());
            if (order == null) return NotFound();

            var rule = StatusRules[order.Status];

            if (rule.IsOwned && order.Driver_id != updOrder.Driver_id)
                return BadRequest($"access denied");

            if (!rule.Next.Contains(newStatus))
                return BadRequest($"only {rule.ShowNext()} after {order.Status}");

            order.Status = updOrder.Status;
            order.Driver_id = updOrder.Driver_id;
            updOrder.Pickup = order.Pickup;
            updOrder.Dropoff = order.Dropoff;
            updOrder.Id = order.Id;

            BrowserSocket.SendToAllAsync(new OrdersSocketMessage
            {
                Payload = Orders
            });

            return Ok(updOrder);
        }

        // DELETE api/orders/5
        [HttpDelete("{id}")]
        public void Delete(string id)
        {
            var order = Orders.SingleOrDefault(o => o.Id == id.ToString());
            if (order == null) return;

            Orders.Remove(order);
        }
    }

    public static class TypeExtensions
    {
        private static readonly ConcurrentDictionary<string, string[]> AllKeys =
            new ConcurrentDictionary<string, string[]>();

        public static string[] GetAllKeys(this Type type)
        {
            // ReSharper disable once AssignNullToNotNullAttribute
            return AllKeys.GetOrAdd(type.FullName, k => type
                .GetFields(BindingFlags.Public | BindingFlags.Static)
                .Select(f => (string)f.GetValue(null))
                .ToArray());
        }
    }
}

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using transporter_api.Extensions;
using transporter_api.WebSockets;

namespace transporter_api.Controllers
{
    [Route("orders")]
    public class OrdersController : Controller
    {
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
        public async Task<IActionResult> Post([FromBody]Order newOrder)
        {
            try
            {
                _orderId++;
                newOrder.Id = _orderId.ToString();
                newOrder.Status = OrderStatus.Unnassigned;
                Orders.Add(newOrder);

                await MobileSocket.SendToAllMobileSockets(new OrderAvailablePayload
                {
                    Payload = newOrder
                });
                return Ok(newOrder);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                throw;
            }
        }

        // PUT api/orders/5
        [HttpPut("{id}")]
        public void Put(int id, [FromBody]string value)
        {

        }

        [HttpPut("{id}/status")]
        public async Task<IActionResult> Put(int id, [FromBody]Order updOrder)
        {
            var order = Orders.SingleOrDefault(o => o.Id == id.ToString());
                
            if (order == null) return NotFound();

            if (!typeof(OrderStatus)
                .GetAllKeys().Contains(updOrder.Status))
                return BadRequest("status field not valid");

            if (!MobileSocket.Drivers.ContainsKey(int.Parse(updOrder.Driver_Id)))
                return BadRequest($"driver_id with id = \"{updOrder.Driver_Id}\" not exists");

            order.Status = updOrder.Status;
            order.Driver_Id = updOrder.Driver_Id;
            updOrder.Pickup = order.Pickup;
            updOrder.Dropoff = order.Dropoff;

            return Ok(updOrder);
        }

        // DELETE api/orders/5
        [HttpDelete("{id}")]
        public void Delete(int id)
        {
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

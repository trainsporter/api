using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using transporter_api.WebSockets;

namespace transporter_api.Controllers
{
    [Route("orders")]
    public class OrdersController : Controller
    {
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

        // DELETE api/orders/5
        [HttpDelete("{id}")]
        public void Delete(int id)
        {
        }
    }
}

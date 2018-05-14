using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using transporter_api.Middleware;
using transporter_api.WebSockets;

namespace transporter_api
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;

            //MobileSocket.StartSendOrders();
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddMvc();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            app.UseMiddleware<ErrorLoggingMiddleware>();

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseWebSockets();


            #region AcceptWebSocket
            app.Use(async (context, next) =>
            {
                if (context.Request.Path == "/mobile")
                {
                    if (!await MobileSocket.TryConnect(context))
                        context.Response.StatusCode = 400;
                }
                else if (context.Request.Path == "/browser")
                {
                    if (!await BrowserSocket.TryConnect(context))
                        context.Response.StatusCode = 400;
                }
                else
                {
                    await next();
                }

            });
            #endregion

            app.UseMvc();
        }
    }

    public class ErrorLoggingMiddleware
    {
        private readonly RequestDelegate _next;

        public ErrorLoggingMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task Invoke(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception e)
            {
                Console.WriteLine($"catched exception in middleware:\r\n{e.ToString()}");
                throw;
            }
        }
    }
}

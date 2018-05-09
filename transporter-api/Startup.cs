﻿using System;
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
                    if (context.WebSockets.IsWebSocketRequest)
                    {
                        var queryDict = QueryHelpers.ParseQuery(context.Request.QueryString.ToString());
                        if (queryDict.TryGetValue("driver_id", out var driverIdString))
                        {
                            if (int.TryParse(driverIdString.ToString(), out int driverId))
                            {
                                WebSocket webSocket = await context.WebSockets.AcceptWebSocketAsync();

                                //if (MobileSocket.MobileWebSockets.TryAdd()
                                await MobileSocket.Connect(context, webSocket, driverId);
                            }
                            else
                            {
                                var s = "driver_id is invalid";
                                byte[] data = Encoding.UTF8.GetBytes(s);
                                await context.Response.Body.WriteAsync(data, 0, data.Length);
                                context.Response.StatusCode = 400;
                            }
                        }
                        else
                        {
                            context.Response.StatusCode = 400;
                        }
                    }
                    else
                    {
                        context.Response.StatusCode = 400;
                    }
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
}

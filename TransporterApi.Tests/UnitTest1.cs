using System;
using transporter_api.WebSockets;
using Xunit;

namespace TransporterApi.Tests
{
    public class UnitTest1
    {
        [Fact]
        public void Test1()
        {
            string answer = MobileSocket.ParseMobileSocketMessage(
@"{""operation"":""position"", ""payload"":{""latitude"": 37.4219983, ""longitude"": -122.081}}");

            Assert.Equal("Hi! Got it, your position: 37,4219983, -122,081", 
                answer);
        }
    }
}

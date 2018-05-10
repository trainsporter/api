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
            var success = MobileSocket.TryParseMobilePosition(
@"{""operation"":""position"", ""payload"":{""latitude"": 37.4219983, ""longitude"": -122.081}}",
            out GeoPoint position);

            Assert.True(success);
            Assert.Equal(37.4219983, position.Latitude);
            Assert.Equal(-122.081, position.Longitude);
        }
    }
}

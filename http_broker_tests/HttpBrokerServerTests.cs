namespace Keel.HttpBroker.Tests
{
    public class HttpBrokerServerTests
    {
        [Fact]
        public void CanCreateServer()
        {
            var server = new HttpBrokerServer();
            Assert.NotNull(server);
        }
    }
}
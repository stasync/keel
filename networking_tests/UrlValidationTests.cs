namespace Core.Networking.Tests
{
    public class UrlValidationTests
    {
        [Fact]
        public void Combine1()
        {
            const string host = "http://172.123.12.24:2355";
            const string baseUrl = $"{host}/";
            const string apiGroup = "/webhooks";
            const string api = "/connected";

            const string expectedResult = $"{host}/webhooks/connected";

            var combinedUrl = Utils.CombineUrl(baseUrl, apiGroup, api);
            Assert.Equal(expectedResult, combinedUrl.TrimEnd('/'));
        }

        [Fact]
        public void Combine2()
        {
            const string host = "https://172.123.12.24:2355";
            const string baseUrl = $"{host}//";
            const string apiGroup = "/webhooks//";
            const string api = "connected/";

            const string expectedResult = $"{host}/webhooks/connected";

            var combinedUrl = Utils.CombineUrl(baseUrl, apiGroup, api);
            Assert.Equal(expectedResult, combinedUrl.TrimEnd('/'));
        }

        [Fact]
        public void Combine3()
        {
            const string host = "http://172.123.12.24:2355";
            const string baseUrl = $"{host}";
            const string apiGroup = "webhooks";
            const string api = "connected";

            const string expectedResult = $"{host}/webhooks/connected/";

            var combinedUrl = Utils.CombineUrl(baseUrl, apiGroup, api);
            Assert.Equal(expectedResult, combinedUrl);
        }

        [Fact]
        public void CombineWithWrongHeader1()
        {
            const string host = "http:/172.123.12.24:2355";
            const string apiGroup = "webhooks";
            Assert.ThrowsAny<Exception>(() => Utils.CombineUrl(host, apiGroup));
        }

        [Fact]
        public void CombineWithWrongHeader2()
        {
            const string host = "172.123.12.24:2355";
            const string apiGroup = "webhooks";
            Assert.ThrowsAny<Exception>(() => Utils.CombineUrl(host, apiGroup));
        }

        [Fact]
        public void ValidateValidUrls()
        {
            var urls = new[]
            {
                "http://172.123.12.24:2355/webhooks/connected/",
                "http://172.123.12.24:2355/webhooks/connected/",
                "https://api.example.com/webhook/",
                "http://localhost:3000/webhook/"
            };

            foreach (var url in urls)
                Assert.True(Utils.IsValidUrl(url));
        }


        [Fact]
        public void ValidateInvalidUrls()
        {
            var urls = new[]
            {
                "https://api.example.com/webhook", // should have tailing '/'.
                "htps://api.example.com/webhook/",
                "ftp://invalid. com/test",
                "not-an-url",
                "http://",
                ""
            };

            foreach (var url in urls)
                Assert.False(Utils.IsValidUrl(url));
        }
    }
}
using System;
using System.Net.Http;
using System.Threading.Tasks;
using NLog;

namespace TeamCityBuildMonitor.Helpers
{
    public class RequestHelper
    {
        private readonly IHttpClientFactory _clientFactory;
        private readonly ILogger _logger;

        public RequestHelper(IHttpClientFactory clientFactory, ILogger logger)
        {
            _clientFactory = clientFactory;
            _logger = logger;
        } 

        public async Task<T> GetJsonAsync<T>(string url) where T : new()
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Add("ts", DateTime.UtcNow.ToFileTime().ToString());
                request.Headers.Add("Accept", "application/json");

                var client = _clientFactory.CreateClient("generic");
                client.Timeout = TimeSpan.FromSeconds(15);
                var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
                var obj = default(T);

                if (response.IsSuccessStatusCode)
                {
                    obj = await response.Content.ReadAsAsync<T>();
                }

                client.Dispose();
                return obj;
            }
            catch(Exception e)
            {
                _logger.Error(e, "TeamCity GET failed.");
                throw;
            }
        }

    }
}
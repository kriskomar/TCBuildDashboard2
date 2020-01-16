using System.Globalization;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Graph;
using Microsoft.IdentityModel.Clients.ActiveDirectory;

namespace TeamCityBuildMonitor.Azure
{
    public class GraphAuthProvider : IGraphAuthProvider
    {
        private GraphServiceClient _graphClient;
        private readonly string _clientId;
        private readonly string _aadInstance;
        private readonly string _tenant;
        private readonly string _appKey;
        private readonly string _graphResourceId;

        public GraphAuthProvider(IConfiguration configuration)
        {
            _clientId = configuration["AzureAd:ClientId"];
            _aadInstance = configuration["AzureAd:Instance"];
            _tenant = configuration["AzureAd:TenantId"];
            _appKey = configuration["AzureAd:AppKey"];
            _graphResourceId = configuration["AzureAd:GraphResourceId"];
        }

        public GraphServiceClient GetAuthenticatedClient()
        {
            _graphClient =  new GraphServiceClient(new DelegateAuthenticationProvider(
                async requestMessage =>
                {
                    var accessToken = await GetAppAccessTokenAsync();
                    requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                    requestMessage.Headers.Add("SampleID", "aspnetcore-connect-sample");
                }));

            return _graphClient;
        }

        public async Task<string> GetAppAccessTokenAsync()
        {
            var authority = string.Format(CultureInfo.InvariantCulture, _aadInstance, _tenant);
            var authContext = new AuthenticationContext(authority);
            var result = await authContext.AcquireTokenAsync(_graphResourceId, new ClientCredential(_clientId, _appKey));

            return result.AccessToken;
        }

    }

    public interface IGraphAuthProvider
    {
        GraphServiceClient GetAuthenticatedClient();
        Task<string> GetAppAccessTokenAsync();
    }
}

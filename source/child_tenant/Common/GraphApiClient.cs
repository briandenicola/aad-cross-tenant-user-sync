using Microsoft.Graph;
using System.Threading.Tasks;
using System.Net.Http.Headers;

using Azure.Core;
using Azure.Identity;

namespace aad.user.sync.common
{
    public static class Helpers {
        public static async Task<GraphServiceClient> GetGraphApiClient()
        {   
            var credential = new ChainedTokenCredential(new ManagedIdentityCredential(), new AzureCliCredential());
            var accessToken = await credential.GetTokenAsync( new 
                TokenRequestContext(scopes: new [] {"https://graph.microsoft.com/.default"}, parentRequestId: null), default);

            var graphServiceClient = new GraphServiceClient(
                new DelegateAuthenticationProvider((requestMessage) =>
            {
                requestMessage
                    .Headers
                    .Authorization = new AuthenticationHeaderValue("bearer", accessToken.Token);

                return Task.CompletedTask;
            }));

            return graphServiceClient;
        }
    }
}


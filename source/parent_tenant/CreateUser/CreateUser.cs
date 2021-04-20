using System;
using System.Threading.Tasks;
using Newtonsoft.Json; 
using Microsoft.Graph; 
using Microsoft.Graph.Core;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;

using aad.user.sync.models;
using aad.user.sync.common;

namespace aad.user.sync
{
    public static class CreateUser
    {
        [FunctionName("CreateUser")]
        public static async Task Run(
            [ServiceBusTrigger(
                "UserCreated", 
                Connection = "servicebus")] string userToCreate, 
            ILogger log)
        {

            log.LogInformation($"C# ServiceBus queue trigger function processed message: {userToCreate}");
            var user = JsonConvert.DeserializeObject<AadGuestUser>( userToCreate );

            var invitation = new Invitation
            {
                InvitedUserEmailAddress = user.UserPrincipalName,
                InvitedUserDisplayName = user.DisplayName,
                InviteRedirectUrl = "https://myapp.microsoft.com"
            };
                        
            var client = await Helpers.GetGraphApiClient();
            var result = await client.Invitations
                                .Request()
                                .AddAsync(invitation);

            log.LogInformation($"User - {user.DisplayName} - was invited as guest user : {result}");
        }
    }
}

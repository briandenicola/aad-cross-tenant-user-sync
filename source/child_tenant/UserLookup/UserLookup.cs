using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.EventHubs;
using Microsoft.Azure.ServiceBus;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

using aad.user.sync.models;
using aad.user.sync.common;

namespace aad.user.sync
{
    public static class UserLookup
    {
        [FunctionName("UserLookup")]
        public static async Task Run(
            [EventHubTrigger("userevents", Connection = "eventhub")] EventData[] events, 
            [ServiceBus("UserCreated", Connection = "servicebus")] IAsyncCollector<AadGuestUser> usersToCreate,
            [ServiceBus("UserDeleted", Connection = "servicebus")] IAsyncCollector<AadGuestUser> usersToDelete,
            ILogger log)
        {
            var exceptions = new List<Exception>();
            var client = await Helpers.GetGraphApiClient();

            foreach (EventData eventData in events)
            {
                try
                {
                    string messageBody = Encoding.UTF8.GetString(eventData.Body.Array, eventData.Body.Offset, eventData.Body.Count);
                    log.LogInformation($"C# Event Hub trigger function processed a message: {messageBody}");
                    
                    var notification = JsonConvert.DeserializeObject<Notifications>( messageBody ).Items.FirstOrDefault();
                    var userId = notification.ResourceData.Id;
                    log.LogInformation($"Update Made to User: {userId}");

                    if( notification.ChangeType == "deleted" ) {
                        var userToCreate = new AadGuestUser()
                        {
                            Id = userId,
                            DisplayName = String.Empty,
                            UserPrincipalName = string.Empty
                        };

                        log.LogInformation($"User Id - {userId} - was sent to other tenant to be deleted.");
                        await usersToDelete.AddAsync(userToCreate);
                    }
                    else {
                        var userDetails = await client.Users[userId]                                        
                                            .Request()
                                            .Select( u => new {
                                                u.Id,
                                                u.DisplayName,
                                                u.UserPrincipalName,
                                                u.CreatedDateTime,
                                                u.UserType
                                            })
                                            .GetAsync();

                        var userToCreate = new AadGuestUser()
                        {
                            Id = userDetails.Id,
                            DisplayName = userDetails.DisplayName,
                            UserPrincipalName = userDetails.UserPrincipalName
                        };

                        log.LogInformation($"User - {userToCreate.UserPrincipalName} - was sent to other tenant to be created as guest user.");
                        await usersToCreate.AddAsync(userToCreate);
                    }
                    await Task.Yield();

                }
                catch(Microsoft.Graph.ClientException ex)
                {
                    log.LogInformation($"Client Exception: {ex.Error} - {ex.Message}.");
                }
                catch (Exception ex)
                {
                    log.LogInformation($"General Exception: {ex.Source} - {ex.Message}.");
                    exceptions.Add(ex);
                }
            }

            if (exceptions.Count > 1)
                throw new AggregateException(exceptions);

            if (exceptions.Count == 1)
                throw exceptions.Single();
        }
    }
}

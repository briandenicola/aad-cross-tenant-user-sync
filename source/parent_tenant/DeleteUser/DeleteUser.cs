using System;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;

namespace parent_tenant
{
    public static class DeleteUser
    {
        [FunctionName("DeleteUser")]
        public static async Task Run(
            [ServiceBusTrigger(
                "UserDeleted", 
                Connection = "servicebus")] string userToDelete, 
            ILogger log)
        {
            log.LogInformation($"C# ServiceBus queue trigger function processed message: {userToDelete}");
        }
    }
}

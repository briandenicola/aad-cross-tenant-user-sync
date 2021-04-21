#r "Microsoft.Azure.EventHubs"
#r "Newtonsoft.Json"

using System;
using System.Configuration;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Xml;
using System.Threading.Tasks;
using Newtonsoft.Json;

using Microsoft.Azure.EventHubs;

const string MsiApiVersion = "2017-09-01";
public static async Task Run(EventData[] events, ILogger log)
{

    string msiEndpoint = Environment.GetEnvironmentVariable("MSI_ENDPOINT");
    string msiSecret = Environment.GetEnvironmentVariable("MSI_SECRET");
    string msiResource = "https://graph.microsoft.com";

    var exceptions = new List<Exception>();

    string tokenString = await GetToken(msiResource, MsiApiVersion, msiEndpoint, msiSecret, log);
    Token token = JsonConvert.DeserializeObject<Token>( tokenString );

    foreach (EventData eventData in events)
    {
        try
        {
            string messageBody = Encoding.UTF8.GetString(eventData.Body.Array, eventData.Body.Offset, eventData.Body.Count);
            var notifications = JsonConvert.DeserializeObject<Notifications>( messageBody );
            log.LogInformation($"Update Made to User: {notifications.Items[0].ResourceData.Id}");

            string user = $"{msiResource}/v1.0/users/{notifications.Items[0].ResourceData.Id}?$select=displayName,userPrincipalName,id,createdDateTime,deletedDateTime,userType";
            string result = await InvokeRestMethodAsync(user, log, HttpMethod.Get, null, token.access_token, "Bearer", null);

            log.LogInformation($"User Detail: {result}");

            await Task.Yield();
        }
        catch (Exception e)
        {
            // We need to keep processing the rest of the batch - capture this exception and continue.
            // Also, consider capturing details of the message that failed processing so it can be processed again later.
            exceptions.Add(e);
        }
    }

    // Once processing of the batch is complete, if any messages in the batch failed processing throw an exception so that there is a record of the failure.

    if (exceptions.Count > 1)
        throw new AggregateException(exceptions);

    if (exceptions.Count == 1)
        throw exceptions.Single();
}

public class Notifications
{
    [JsonProperty(PropertyName = "value")]
    public Notification[] Items { get; set; }
}

public class Notification
{
    // The type of change.
    [JsonProperty(PropertyName = "changeType")]
    public string ChangeType { get; set; }

    // The client state used to verify that the notification is from Microsoft Graph. Compare the value received with the notification to the value you sent with the subscription request.
    [JsonProperty(PropertyName = "clientState")]
    public string ClientState { get; set; }

    // The endpoint of the resource that changed. For example, a message uses the format ../Users/{user-id}/Messages/{message-id}
    [JsonProperty(PropertyName = "resource")]
    public string Resource { get; set; }

    // The UTC date and time when the webhooks subscription expires.
    [JsonProperty(PropertyName = "subscriptionExpirationDateTime")]
    public DateTimeOffset SubscriptionExpirationDateTime { get; set; }

    // The unique identifier for the webhooks subscription.
    [JsonProperty(PropertyName = "subscriptionId")]
    public string SubscriptionId { get; set; }

    // Properties of the changed resource.
    [JsonProperty(PropertyName = "resourceData")]
    public ResourceData ResourceData { get; set; }
}

public class ResourceData
{
    // The ID of the resource.
    [JsonProperty(PropertyName = "id")]
    public string Id { get; set; }

    // The OData etag property.
    [JsonProperty(PropertyName = "@odata.etag")]
    public string ODataEtag { get; set; }

    // The OData ID of the resource. This is the same value as the resource property.
    [JsonProperty(PropertyName = "@odata.id")]
    public string ODataId { get; set; }

    // The OData type of the resource: "#Microsoft.Graph.Message", "#Microsoft.Graph.Event", or "#Microsoft.Graph.Contact".
    [JsonProperty(PropertyName = "@odata.type")]
    public string ODataType { get; set; }
}

public static async Task<string> GetToken(string resource, string apiversion, string msiEndpoint, string msiSecret, ILogger log)
{
    string msiUrl = $"{msiEndpoint}?resource={resource}&api-version={apiversion}";
    log.LogInformation($"MSI Endpoint={msiEndpoint}");
    log.LogInformation($"MSI Url={msiUrl}");

    var headers = new Dictionary<string, string>();
    headers.Add("Secret", msiSecret);
    var tokenPayload = await InvokeRestMethodAsync(msiUrl, log, HttpMethod.Get, null, null, null, headers);
    log.LogInformation($"Token Payload={tokenPayload}");

    return tokenPayload;
}

public static async Task<string> InvokeRestMethodAsync(string url, ILogger log, HttpMethod httpMethod, string body = null, string authorizationToken = null, string authorizationScheme = "Bearer", IDictionary<string, string> headers = null)
{
    HttpClient client = new HttpClient();
    if (!string.IsNullOrWhiteSpace(authorizationToken))
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(authorizationScheme, authorizationToken);
        log.LogInformation($"Authorization: {client.DefaultRequestHeaders.Authorization.Parameter}");
    }
    
    HttpRequestMessage request = new HttpRequestMessage(httpMethod, url);
    if (headers != null && headers.Count > 0)
    {
        foreach (var header in headers)
        {
            request.Headers.Add(header.Key, header.Value);
        }
    }

    if (!string.IsNullOrWhiteSpace(body))
    {
        request.Content = new StringContent(body, Encoding.UTF8, "application/json");
    }

    HttpResponseMessage response = await client.SendAsync(request);
    if (response.IsSuccessStatusCode)
    {
        return await response.Content.ReadAsStringAsync();
    }

    string statusCodeName = response.StatusCode.ToString();
    int statusCodeValue = (int)response.StatusCode;
    string content = await response.Content.ReadAsStringAsync();
    log.LogInformation($"Status Code: {statusCodeName} ({statusCodeValue}). Body: {content}");

    throw new Exception($"Status Code: {statusCodeName} ({statusCodeValue}). Body: {content}");
}

public class Token
{
    public string access_token { get; set; }
    public DateTime expires_on { get; set; }
    public string resource { get; set; }
    public string token_type { get; set; }
}

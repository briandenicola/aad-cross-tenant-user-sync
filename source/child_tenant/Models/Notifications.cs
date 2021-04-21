using System;
using Newtonsoft.Json;

namespace aad.user.sync.models {
    public class Notifications
    {
        [JsonProperty(PropertyName = "value")]
        public Notification[] Items { get; set; }
    }

    public class Notification
    {
        [JsonProperty(PropertyName = "changeType")]
        public string ChangeType { get; set; }

        [JsonProperty(PropertyName = "clientState")]
        public string ClientState { get; set; }

        [JsonProperty(PropertyName = "resource")]
        public string Resource { get; set; }

        [JsonProperty(PropertyName = "subscriptionExpirationDateTime")]
        public DateTimeOffset SubscriptionExpirationDateTime { get; set; }

        [JsonProperty(PropertyName = "subscriptionId")]
        public string SubscriptionId { get; set; }

        [JsonProperty(PropertyName = "resourceData")]
        public ResourceData ResourceData { get; set; }
    }

    public class ResourceData
    {
        [JsonProperty(PropertyName = "id")]
        public string Id { get; set; }

        [JsonProperty(PropertyName = "@odata.etag")]
        public string ODataEtag { get; set; }

        [JsonProperty(PropertyName = "@odata.id")]
        public string ODataId { get; set; }

        [JsonProperty(PropertyName = "@odata.type")]
        public string ODataType { get; set; }
    }
}
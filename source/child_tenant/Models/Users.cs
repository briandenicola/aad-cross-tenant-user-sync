using Newtonsoft.Json;

namespace aad.user.sync.models {
    public class AadGuestUser 
    {
        [JsonProperty(PropertyName = "id")]
        public string Id { get; set; }
    
        [JsonProperty(PropertyName = "displayName")]
        public string DisplayName { get; set; }

        [JsonProperty(PropertyName = "userPrincipalName")]
        public string UserPrincipalName { get; set; }

    }
}
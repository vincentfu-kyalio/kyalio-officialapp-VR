using System.Collections.Generic;
using Newtonsoft.Json;

namespace Kyalio.Models
{
    public class SubscriptionResolveRequest
    {
        [JsonProperty("subscriptionIds")]
        public List<string> SubscriptionIds;
    }

    public class SubscriptionResolveResponse
    {
        [JsonProperty("items")]
        public List<SubscriptionItem> Items;

        [JsonProperty("notFoundIds")]
        public List<string> NotFoundIds;
    }

    public class SubscriptionItem
    {
        [JsonProperty("id")]
        public string Id;

        [JsonProperty("name")]
        public string Name;

        [JsonProperty("description")]
        public string Description;

        [JsonProperty("active")]
        public int Active;

        [JsonProperty("projects")]
        public List<SubscribedProject> Projects;

        [JsonProperty("categories")]
        public List<Category> Categories;
    }

    public class Category
    {
        [JsonProperty("id")]
        public string Id;

        [JsonProperty("name")]
        public string Name;

        [JsonProperty("picUrl")]
        public string PicUrl;
    }
}

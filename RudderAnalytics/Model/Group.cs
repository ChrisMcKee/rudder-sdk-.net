using System.Collections.Generic;
using Newtonsoft.Json;

namespace RudderStack.Model
{
    public class Group : BaseAction
    {
        [JsonProperty(PropertyName = "groupId")]
        private string GroupId { get; set; }

        [JsonProperty(PropertyName = "traits")]
        private IDictionary<string, object> Traits { get; set; }

        internal Group(string userId,
                       string groupId,
                       IDictionary<string, object> traits,
                       RudderOptions options)
            : base("group", userId, options)
        {
            this.GroupId = groupId;
            this.Traits = traits ?? new Traits();
        }
    }
}

using System.Collections.Generic;

namespace RudderStack.Model
{
    public class RudderContext : Dictionary<string,object>
    {
        /// <summary>
        /// Provides additional information about the context of an analytics call,
        /// such as the visitor's ip or language.
        /// </summary>
        public RudderContext() {
            // default the context library
            this.Add("library", new Dictionary<string,object> {
                { "name", "RudderAnalytics.NET" },
                { "version", RudderAnalytics.VERSION }
            });
        }

        public new RudderContext Add(string key, object val) {
            base.Add(key, val);
            return this;
        }
    }
}
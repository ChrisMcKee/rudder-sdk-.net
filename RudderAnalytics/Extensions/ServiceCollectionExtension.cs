using Microsoft.Extensions.DependencyInjection;

namespace RudderStack.Extensions
{
    public static class ServiceCollectionExtension
    {
        public static void AddAnalytics(this IServiceCollection services, string writeKey, RudderConfig config = null)
        {
            RudderConfig configuration;

            configuration = config ?? new RudderConfig();

            var client = new RudderClient(writeKey, configuration);
            services.AddSingleton<IRudderAnalyticsClient>(client);
        }
    }
}
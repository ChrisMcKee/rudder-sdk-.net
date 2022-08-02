using System;

namespace RudderStack.Test
{
    class Program
    {
        static void Main_Exe(string[] args)
        {
            Logger.Handlers += Logger_Handlers;

            //Analytics.Initialize(RudderStack.Test.Constants.WRITE_KEY);

            //FlushTests tests = new FlushTests();
            //tests.PerformanceTest().Wait();
            RudderAnalytics.Initialize("1sCR76JzHpQohjl33pi8qA5jQD2", new RudderConfig(dataPlaneUrl: "https://75652af01e6d.ngrok.io"));
            RudderAnalytics.Client.Track("prateek", "Item Purchased");
            RudderAnalytics.Client.Flush();
        }

        private static void Logger_Handlers(Logger.Level level, string message, string[,] args)
        {
            Console.WriteLine(message);
        }
    }
}
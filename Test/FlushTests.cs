using System;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using NUnit.Framework;
using RudderStack.Model;
using RudderStack.Request;

namespace RudderStack.Test
{
    [TestFixture()]
    public class FlushTests
    {
        private Mock<IRequestHandler> _mockRequestHandler;

        [SetUp]
        public void Init()
        {
            _mockRequestHandler = new Mock<IRequestHandler>();
            _mockRequestHandler
                .Setup(x => x.MakeRequest(It.IsAny<Batch>()))
                .Returns((Batch b) =>
                {
                    b.batch.ForEach(_ => RudderAnalytics.Client.Statistics.IncrementSucceeded());
                    return Task.CompletedTask;
                });

            RudderAnalytics.Dispose();
            Logger.Handlers += LoggingHandler;
        }

        [TearDown]
        public void CleanUp()
        {
            Logger.Handlers -= LoggingHandler;
        }

        [Test()]
        public void SynchronousFlushTest()
        {
            var client = new RudderClient(Constants.WRITE_KEY, new RudderConfig().SetAsync(false),
                _mockRequestHandler.Object);
            RudderAnalytics.Initialize(client);
            RudderAnalytics.Client.Succeeded += Client_Succeeded;
            RudderAnalytics.Client.Failed += Client_Failed;

            int trials = 10;

            RunTests(RudderAnalytics.Client, trials);

            RudderAnalytics.Client.Flush();

            Assert.AreEqual(trials, RudderAnalytics.Client.Statistics.Submitted);
            Assert.AreEqual(trials, RudderAnalytics.Client.Statistics.Succeeded);
            Assert.AreEqual(0, RudderAnalytics.Client.Statistics.Failed);
        }

        [Test()]
        public void AsynchronousFlushTest()
        {
            var client = new RudderClient(Constants.WRITE_KEY, new RudderConfig().SetAsync(true),
                _mockRequestHandler.Object);
            RudderAnalytics.Initialize(client);

            RudderAnalytics.Client.Succeeded += Client_Succeeded;
            RudderAnalytics.Client.Failed += Client_Failed;

            int trials = 10;

            RunTests(RudderAnalytics.Client, trials);

            RudderAnalytics.Client.Flush();

            Assert.AreEqual(trials, RudderAnalytics.Client.Statistics.Submitted);
            Assert.AreEqual(trials, RudderAnalytics.Client.Statistics.Succeeded);
            Assert.AreEqual(0, RudderAnalytics.Client.Statistics.Failed);
        }

        [Test()]
        public async Task PerformanceTest()
        {
            var client = new RudderClient(Constants.WRITE_KEY, new RudderConfig(), _mockRequestHandler.Object);
            RudderAnalytics.Initialize(client);

            RudderAnalytics.Client.Succeeded += Client_Succeeded;
            RudderAnalytics.Client.Failed += Client_Failed;

            int trials = 100;

            DateTime start = DateTime.Now;

            Console.WriteLine("starting to trigger events");
            RunTests(RudderAnalytics.Client, trials);
            Console.WriteLine("finishing to trigger events");

            await RudderAnalytics.Client.FlushAsync();

            TimeSpan duration = DateTime.Now.Subtract(start);

            Assert.AreEqual(trials, RudderAnalytics.Client.Statistics.Submitted);
            Assert.AreEqual(trials, RudderAnalytics.Client.Statistics.Succeeded);
            Assert.AreEqual(0, RudderAnalytics.Client.Statistics.Failed);

            Assert.IsTrue(duration.CompareTo(TimeSpan.FromSeconds(20)) < 0);
        }

        private void RunTests(RudderClient client, int trials)
        {
            for (int i = 0; i < trials; i += 1)
            {
                Actions.Random(client);
                if (i % 100 == 0) Thread.Sleep(500);
            }
        }

        void Client_Failed(BaseAction action, System.Exception e)
        {
            Console.WriteLine(string.Format("Action [{0}] {1} failed : {2}",
                action.MessageId, action.Type, e.Message));
        }

        void Client_Succeeded(BaseAction action)
        {
            Console.WriteLine(string.Format("Action [{0}] {1} succeeded.",
                action.MessageId, action.Type));
        }

        static void LoggingHandler(Logger.Level level, string message, string[,] args)
        {
            if (args != null)
            {
                for (var i = 0; i < args.GetLength(0); i++)
                {
                    message += string.Format(" {0}: {1},", "" + args[i,0], "" + args[i,1]);
                }
            }

            Console.WriteLine("[FlushTests] [{0}] {1}", level, message);
        }
    }
}
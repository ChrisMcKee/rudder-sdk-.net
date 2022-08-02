using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Moq;
using NUnit.Framework;
using RudderStack.Model;
using RudderStack.Request;

namespace RudderStack.Test
{
    [TestFixture()]
    public class ConnectionTests
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
        public void RetryErrorTest()
        {
            Stopwatch watch = new Stopwatch();

            // Set invalid host address and make timeout to 1s
            var config = new RudderConfig().SetAsync(false);
            config.SetHost("https://fake.rudder-server.com");
            config.SetTimeout(new TimeSpan(0, 0, 1));
            config.SetMaxRetryTime(new TimeSpan(0, 0, 10));
            RudderAnalytics.Initialize(Constants.WRITE_KEY, config);
            // Calculate working time for Identity message with invalid host address
            watch.Start();
            Actions.Identify(RudderAnalytics.Client);
            watch.Stop();

            Assert.AreEqual(1, RudderAnalytics.Client.Statistics.Submitted);
            Assert.AreEqual(0, RudderAnalytics.Client.Statistics.Succeeded);
            Assert.AreEqual(1, RudderAnalytics.Client.Statistics.Failed);

            // Handling Identify message will take more than 5s even though the timeout is 1s.
            // That's because it retries submit when it's failed.
            Assert.AreEqual(true, watch.ElapsedMilliseconds > 10000);
        }

        [Test()]
        public void RetryErrorWithDefaultMaxRetryTimeTest()
        {
            Stopwatch watch = new Stopwatch();

            // Set invalid host address and make timeout to 1s
            var config = new RudderConfig().SetAsync(false);
            config.SetHost("https://fake.rudder-server.com");
            config.SetTimeout(new TimeSpan(0, 0, 1));
            RudderAnalytics.Initialize(Constants.WRITE_KEY, config);
            // Calculate working time for Identiy message with invalid host address
            watch.Start();
            Actions.Identify(RudderAnalytics.Client);
            watch.Stop();

            Assert.AreEqual(1, RudderAnalytics.Client.Statistics.Submitted);
            Assert.AreEqual(0, RudderAnalytics.Client.Statistics.Succeeded);
            Assert.AreEqual(1, RudderAnalytics.Client.Statistics.Failed);

            // Handling Identify message will take more than 5s even though the timeout is 1s.
            // That's because it retries submit when it's failed.
            Assert.AreEqual(true, watch.ElapsedMilliseconds > 10000);
        }

        [Test()]
        public void ProxyTest()
        {
            // Set proxy address, like as "http://localhost:8888"
            var client = new RudderClient(Constants.WRITE_KEY, new RudderConfig().SetAsync(false).SetProxy(""), _mockRequestHandler.Object);
            RudderAnalytics.Initialize(client);

            Actions.Identify(RudderAnalytics.Client);

            Assert.AreEqual(1, RudderAnalytics.Client.Statistics.Submitted);
            Assert.AreEqual(1, RudderAnalytics.Client.Statistics.Succeeded);
            Assert.AreEqual(0, RudderAnalytics.Client.Statistics.Failed);
        }

        [Test()]
        public void GZipTest()
        {
            // Set GZip/Deflate on request header
            var client = new RudderClient(Constants.WRITE_KEY, new RudderConfig().SetAsync(false), _mockRequestHandler.Object);
            RudderAnalytics.Initialize(client);

            Actions.Identify(RudderAnalytics.Client);

            Assert.AreEqual(1, RudderAnalytics.Client.Statistics.Submitted);
            Assert.AreEqual(1, RudderAnalytics.Client.Statistics.Succeeded);
            Assert.AreEqual(0, RudderAnalytics.Client.Statistics.Failed);
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
            Console.WriteLine(string.Format("[ConnectionTest] [{0}] {1}", level, message));
        }
    }
}
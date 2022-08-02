namespace RudderStack.Test
{
	[TestClass]
	public class ActionTests
	{
		private Mock<IRequestHandler> _mockRequestHandler;

		[TestInitialize]
		public void Init()
		{
			_mockRequestHandler = new Mock<IRequestHandler>();
			_mockRequestHandler
				.Setup(x => x.MakeRequest(It.IsAny<Batch>()))
				.Returns((Batch b) =>
				{
					b.batch.ForEach(_ => Analytics.Client.Statistics.IncrementSucceeded());
					return Task.CompletedTask;
				});

			Analytics.Dispose();
			Logger.Handlers += LoggingHandler;
			var client = new Client(Constants.WRITE_KEY, new Config().SetAsync(false), _mockRequestHandler.Object);
			Analytics.Initialize(client);
		}

        [TestCleanup]
        public void CleanUp()
        {
            Logger.Handlers -= LoggingHandler;
        }

		[TestMethod]
		public void IdentifyTestNetPortable()
		{
			Actions.Identify(Analytics.Client);
			FlushAndCheck(1);
		}

		[TestMethod]
		public void TrackTestNetPortable()
		{
			Actions.Track(Analytics.Client);
			FlushAndCheck(1);
		}

		[TestMethod]
		public void AliasTestNetPortable()
		{
			Actions.Alias(Analytics.Client);
			FlushAndCheck(1);
		}

		[TestMethod]
		public void GroupTestNetPortable()
		{
			Actions.Group(Analytics.Client);
			FlushAndCheck(1);
		}

		[TestMethod]
		public void PageTestNetPortable()
		{
			Actions.Page(Analytics.Client);
			FlushAndCheck(1);
		}

		[TestMethod]
		public void ScreenTestNetPortable()
		{
			Actions.Screen(Analytics.Client);
			FlushAndCheck(1);
		}

		private void FlushAndCheck(int messages)
		{
			Analytics.Client.Flush();
			Assert.AreEqual(messages, Analytics.Client.Statistics.Submitted);
			Assert.AreEqual(messages, Analytics.Client.Statistics.Succeeded);
			Assert.AreEqual(0, Analytics.Client.Statistics.Failed);
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
			Debug.WriteLine(String.Format("[ActionTests] [{0}] {1}", level, message));
		}
	}
}
namespace RudderStack.Test
{
	[TestClass]
	public class ClientTests
	{
		Client client;

		[TestInitialize]
		public void Init()
		{
			client = new Client("foo");
		}

		[TestMethod]
		public void TrackTestNetPortable()
		{
			// verify it doesn't fail for a null options
			client.Screen("bar", "qaz", null, null);
		}
	}
}


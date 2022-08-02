namespace RudderStack.Exception
{
    public class NotInitializedException : System.Exception
    {
        public NotInitializedException() : base("Please initialize RudderStack first before using.") { }

    }
}
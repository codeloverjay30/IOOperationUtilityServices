using System;

namespace IOOperation.BaseUtilityServices
{
    public class LockTimeoutException : Exception
    {
        public LockTimeoutException() : base("The lock acquisition timed out.") { }
        public LockTimeoutException(string message) : base(message) { }
        public LockTimeoutException(string message , Exception innerException)
            : base(message , innerException)
        {
        }
    }
}

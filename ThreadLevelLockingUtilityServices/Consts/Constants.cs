using System;
using System.Collections.Generic;
using System.Text;

namespace ThreadLevelLockingUtilityServices.Consts
{
    public static class Constants
    {
        public static class CircuitBreaker
        {
            public const string IsClosed = "The circuit breaker is closed!!!";
            public const string IsOpened = "The circuit breaker is Opened!!!";
        }

        public static class Requests
        {
            public const string RequestsDenied = "Request Denied!!!";
            public const string RequestsOk = "Request Okay!!!";
        }
    }
}

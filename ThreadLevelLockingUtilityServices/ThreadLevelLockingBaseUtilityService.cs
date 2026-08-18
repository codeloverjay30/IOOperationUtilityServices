using LoggerFactoryUtilityServices;
using System;
using System.Collections.Generic;
using System.Text;

namespace ThreadLevelLockingUtilityServices
{
    public class ThreadLevelLockingBaseUtilityService(
        ILoggerFactoryBaseUtilityService loggerFactoryService
    ): ThreadLevelLockingAbstractBaseUtilityService(loggerFactoryService)
    {
    }
}

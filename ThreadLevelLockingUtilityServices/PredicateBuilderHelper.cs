using Polly;
using System;
using System.Collections.Generic;
using System.Text;

namespace ThreadLevelLockingUtilityServices
{
    public static class PredicateBuilderHelper
    {
        public static PredicateBuilder<TType> AppendExceptionPredicate<TType>(
            this PredicateBuilder<TType> predicateBuilder ,
            IEnumerable<Type> additionalExceptionTypes
        )
        {
            var types = additionalExceptionTypes ?? Enumerable.Empty<Type>();
            if(additionalExceptionTypes != null)
            {
                foreach(var type in additionalExceptionTypes)
                {
                    // 僅在必要時進行反射，且儘量使用快取的類型處理
                    predicateBuilder.Handle<Exception>(ex => ex.GetType() == type || ex.InnerException?.GetType() == type);
                }
            }
            return predicateBuilder;
        }
    }
}

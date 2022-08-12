using System.Collections.Generic;

namespace PhoneBox
{
    internal static class EnumerableExtensions
    {
        // Array is allocated and the enumerable is not enumerated lazy
        //public static IEnumerable<T> Create<T>(params T[] args) => args;
        public static IEnumerable<T> Create<T>(T arg1)
        {
            yield return arg1;
        }
        public static IEnumerable<T> Create<T>(T arg1, T arg2)
        {
            yield return arg1;
            yield return arg2;
        }
        public static IEnumerable<T> Create<T>(T arg1, T arg2, T arg3)
        {
            yield return arg1;
            yield return arg2;
            yield return arg3;
        }
        public static IEnumerable<T> Create<T>(T arg1, T arg2, T arg3, T arg4)
        {
            yield return arg1;
            yield return arg2;
            yield return arg3;
            yield return arg4;
        }
        public static IEnumerable<T> Create<T>(T arg1, T arg2, T arg3, T arg4, T arg5)
        {
            yield return arg1;
            yield return arg2;
            yield return arg3;
            yield return arg4;
            yield return arg5;
        }
    }
}
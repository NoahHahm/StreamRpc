namespace StreamRpc
{
    using System;
    using System.Threading.Tasks;

    internal class Requires
    {
        public static void Argument(bool condition, string parameterName, string message = null)
        {
            if (!condition)
                throw message != null ? new ArgumentException(message, parameterName) : new ArgumentException(parameterName);
        }

        public static void Range(bool condition, string parameterName, string message = null)
        {
            if (!condition)
                throw message != null ? new ArgumentOutOfRangeException(message, parameterName) : new ArgumentOutOfRangeException(parameterName);
        }

        public static T NotNull<T>(T obj, string parameterName)
            where T : class
        {
            if (obj == null)
                throw new ArgumentNullException(parameterName);
            return obj;
        }

        public static void NotNull(Task value, string parameterName)
        {
            if (value == null)
                throw new ArgumentNullException(parameterName);
        }

        public static void NotNull<T>(Task<T> value, string parameterName)
        {
            if (value == null)
                throw new ArgumentNullException(parameterName);
        }

        public static void NotNullOrEmpty(string obj, string parameterName)
        {
            if (obj == null)
                throw new ArgumentNullException(parameterName);
            if (string.IsNullOrWhiteSpace(obj))
                throw new ArgumentException(parameterName, $"{parameterName} should not be empty");
        }

        public static void Fail(string format, params object[] args)
        {
            throw new ArgumentException(string.Format(format, args));
        }
    }
}
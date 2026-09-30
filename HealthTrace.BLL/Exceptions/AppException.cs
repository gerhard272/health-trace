using System;
using System.Collections.Generic;
using System.Text;

namespace HealthTrace.BLL.Exceptions
{
    /// <summary>
    /// Represents a custom application exception.
    /// All application-specific exceptions
    /// should derive from this class.
    /// Abstract to prevent direct instantiation.
    /// </summary>
    public abstract class AppException : Exception
    {
        public AppException() { }

        public AppException(string message) : base(message) { }

        public AppException(string message, Exception innerException) : base(message, innerException) { }
    }
}

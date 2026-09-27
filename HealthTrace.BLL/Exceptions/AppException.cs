using System;
using System.Collections.Generic;
using System.Text;

namespace HealthTrace.BLL.Exceptions
{
    /// <summary>
    /// Rappresenta un'eccezione personalizzata per l'applicazione.
    /// Tutte le eccezioni specifiche dell'applicazione
    /// dovrebbero derivare da questa classe.
    /// Astratta per impedire l'istanza diretta.
    /// </summary>
    public abstract class AppException : Exception
    {
        public AppException() { }

        public AppException(string message) : base(message) { }

        public AppException(string message, Exception innerException) : base(message, innerException) { }
    }
}

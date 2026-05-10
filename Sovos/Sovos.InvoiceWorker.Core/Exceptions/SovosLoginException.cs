namespace Sovos.InvoiceWorker.Core.Exceptions;

public class SovosLoginException : Exception
{
    public SovosLoginException(string message) : base(message) { }

    public SovosLoginException(string message, Exception innerException)
        : base(message, innerException) { }
}

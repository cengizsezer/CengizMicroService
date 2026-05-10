namespace Sovos.InvoiceWorker.Core.Exceptions;

public class SovosCaptchaActiveException : SovosLoginException
{
    public SovosCaptchaActiveException()
        : base("Captcha aktif, manuel müdahale gerekli.") { }

    public SovosCaptchaActiveException(string message)
        : base(message) { }

    public SovosCaptchaActiveException(string message, Exception innerException)
        : base(message, innerException) { }
}

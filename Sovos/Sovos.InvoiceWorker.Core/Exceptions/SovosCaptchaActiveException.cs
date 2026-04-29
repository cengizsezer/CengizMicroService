namespace Sovos.InvoiceWorker.Core.Exceptions;

public class SovosCaptchaActiveException : Exception
{
    public SovosCaptchaActiveException() : base("Captcha aktif, manuel müdahale gerekli.") { }
}

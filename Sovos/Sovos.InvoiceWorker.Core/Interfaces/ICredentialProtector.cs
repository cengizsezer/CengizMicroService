namespace Sovos.InvoiceWorker.Core.Interfaces;

public interface ICredentialProtector
{
    string Encrypt(string plain);
    string Decrypt(string cipher);
}

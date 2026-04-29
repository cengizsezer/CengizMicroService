using Microsoft.AspNetCore.DataProtection;
using Sovos.InvoiceWorker.Core.Interfaces;

namespace SovosService.Api.Services;

public class CredentialProtector : ICredentialProtector
{
    private readonly IDataProtector _protector;

    public CredentialProtector(IDataProtectionProvider provider)
    {
        _protector = provider.CreateProtector("SovosInvoiceWorker.Credentials");
    }

    public string Encrypt(string plain) => _protector.Protect(plain);
    public string Decrypt(string cipher) => _protector.Unprotect(cipher);
}

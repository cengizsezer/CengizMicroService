namespace CatalogService.Api.Contracts.Dtos
{
    public class PersonnelDto
    {
        public int Id { get; set; }

        public string NationalId { get; set; } = string.Empty;     // TC NO
        public string FirstName { get; set; } = string.Empty;      // Adı
        public string LastName { get; set; } = string.Empty;       // Soyadı
        public string FullName => $"{FirstName} {LastName}";       // Otomatik ad + soyad

        public string Title { get; set; } = string.Empty;          // Ünvan
        public string PhoneNumber { get; set; } = string.Empty;    // Cep Telefonu
        public string Email { get; set; } = string.Empty;          // Mail Adresi
        public string IBAN { get; set; } = string.Empty;           // IBAN

        public string Company { get; set; } = string.Empty;        // Kurum
        public string Department { get; set; } = string.Empty;     // Bölüm
        public string Unit { get; set; } = string.Empty;           // Birim (örnek: İstanbul)
        public string ExpenseCenter { get; set; } = string.Empty;  // Masraf Merkezi
    }
}

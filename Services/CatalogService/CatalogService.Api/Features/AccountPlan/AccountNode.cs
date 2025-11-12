namespace CatalogService.Api.Features.AccountPlan
{
    public class AccountNode
    {
        public int Id { get; set; }
        public string Code { get; set; } = "";     // 100, 101, 10, 1...
        public string Name { get; set; } = "";     // Kasa, Bankalar...
        public string? Description { get; set; }   // İsteğe bağlı açıklama
        public string? Notes { get; set; }         // Senin yazacağın notlar
        public int Level { get; set; }             // 1=Ana grup, 2=Alt grup, 3=Hesap
        public int? ParentId { get; set; }
        public AccountNode? Parent { get; set; }
        public List<AccountNode> Children { get; set; } = new();
        public int Order { get; set; }             // Sıralama için
    }
}

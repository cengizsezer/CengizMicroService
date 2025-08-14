namespace IdentityService.Domain.Entities
{
    public class Firm
    {
        public int Id { get; set; }
        public string Ad { get; set; } = string.Empty;
        public string Vkn { get; set; } = string.Empty;
        public string FirmaNo { get; set; } = string.Empty;

        public ICollection<UserFirm> UserFirmalar { get; set; } = new List<UserFirm>();
    }
}

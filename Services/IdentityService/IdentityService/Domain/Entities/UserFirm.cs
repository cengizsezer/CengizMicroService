namespace IdentityService.Domain.Entities
{
    public class UserFirm
    {
        public int UserId { get; set; }              // <-- int olmalı
        public User User { get; set; } = null!;

        public int FirmaId { get; set; }
        public Firm Firma { get; set; } = null!;
    }
}

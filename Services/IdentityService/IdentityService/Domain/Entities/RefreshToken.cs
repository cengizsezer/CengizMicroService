namespace IdentityService.Domain.Entities
{
    public class RefreshToken
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string Token { get; set; } = default!;
        public DateTime ExpiresAtUtc { get; set; }
        public bool IsUsed { get; set; }
        public bool IsRevoked { get; set; }
        public string? DeviceId { get; set; }
        public string? IpAddress { get; set; }
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    }
}

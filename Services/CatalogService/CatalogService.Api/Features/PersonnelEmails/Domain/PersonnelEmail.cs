namespace CatalogService.Api.Features.PersonnelEmails.Domain
{
    /// <summary>
    /// Kullanıcı (IdentityService user id) → e-posta eşlemesi.
    /// Global'dir: tenant'a bağlı DEĞİL (bir kullanıcının e-postası firmadan bağımsızdır).
    /// Görev atamada JobService bu tabloyu assignee UserId'si ile okuyup ek alıcı çözer.
    /// </summary>
    public class PersonnelEmail
    {
        // Assignee dropdown'ı ile aynı id (IdentityService user id, string).
        public string UserId { get; set; } = default!;
        public string? UserName { get; set; }
        public string? Email { get; set; }
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}

namespace WebApp.Shared.Dto.Admin
{
    public class FirmDto { public int Id { get; set; } public string Name { get; set; } = ""; public string FirmaNo { get; set; } = ""; }

    public class SetUserFirmsRequest
    {
        public List<UserFirmAssignDto> Firms { get; set; } = new();
    }

    public class UserFirmAssignDto
    {
        public int TenantId { get; set; }
        public List<string>? Roles { get; set; }   // null => rol dokunma, [] => rolleri boşalt
    }


    public class UserFirmDto
    {
        public int TenantId { get; set; }
        public string TenantName { get; set; } = "";
        public List<string> Roles { get; set; } = new();
    }

}

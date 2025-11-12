using static WebApp.Pages.AccountNodePlan.AccountNodePlanPage;

namespace WebApp.Shared.Dto.AccountNodePlan
{
    public class AccountNodeDto
    {
        public int Id { get; set; }
        public string Code { get; set; } = "";
        public string Name { get; set; } = "";
        public string? Description { get; set; }
        public string? Notes { get; set; }
        public int Level { get; set; }
        public int? ParentId { get; set; }
        public List<AccountNodeDto> Children { get; set; } = new(); // << ÖNEMLİ
        public int Order { get; set; }
    }
}

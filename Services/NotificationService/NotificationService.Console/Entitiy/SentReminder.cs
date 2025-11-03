using NotificationService.Console.Entitiy;

public class SentReminder
{
    public Guid Id { get; set; }
    public Guid TaskId { get; set; }

    // ESKİ: public Guid UserId { get; set; }
    public string UserId { get; set; } = "";   // <-- string

    public byte Kind { get; set; }             // 0:Before, 1:OnDay
    public DateOnly SendDate { get; set; }     // Istanbul local date
    public DateTime SentAtUtc { get; set; }
   
}

namespace WebApp.Domain.Models.FirmaKontrol
{
    public class ControlItem
    {
        public int Id { get; set; }

        /// <summary>
        /// Şablon maddesi için stabil anahtar (örn "DV-01"). Durum/not bu anahtara
        /// bağlanır — DB'ye kaydederken sıra numarası/Id değil bu kullanılır.
        /// Özel maddede null (onlar <see cref="Id"/> ile tanımlanır).
        /// </summary>
        public string? MaddeKey { get; set; }

        /// <summary>Firmaya özel (kullanıcı eklemiş) madde mi?</summary>
        public bool IsCustom { get; set; }

        public string Category { get; set; } = string.Empty;
        public string Question { get; set; } = string.Empty;
        public bool IsChecked { get; set; }
        public ControlStatus Status { get; set; } = ControlStatus.Pending;
        public string? Note { get; set; }
    }
}

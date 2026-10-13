using Radzen;

namespace WebApp.Pages.Yapilacaklar
{
    /// <summary>
    /// Firma Panosu pencerelerinin Radzen seçenekleri: başlık çubuğu yok (pencere kendi .ck-dlg-ust'unu çizer), iç
    /// boşluk yok, köşeler yuvarlak. ChecklistKartDialog'un seçenekleriyle aynı.
    /// </summary>
    public static class CkPencere
    {
        public static DialogOptions Secenekler(string genislik) => new()
        {
            ShowTitle = false,
            Width = genislik,
            Style = "--rz-dialog-content-padding:0; padding:0; border-radius:14px; overflow:hidden",
            CloseDialogOnOverlayClick = false,
            CloseDialogOnEsc = true
        };
    }
}

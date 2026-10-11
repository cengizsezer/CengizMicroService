namespace WebApp.Shared.Dto.MevzuatGrafigi
{
    /// <summary>
    /// İstemciye özgü çağrı sonucu (sunucuda karşılığı yok). Başarısızlıkta <see cref="Mesaj"/>
    /// sunucunun 400/404 gövdesindeki { message } metnidir. Sayfa bunu JS'e olduğu gibi geçirir;
    /// JS değişikliği geri alıp mesajı toast'lar.
    /// </summary>
    public class MevzuatGrafigiSonuc
    {
        public bool Basarili { get; set; }
        public string? Mesaj { get; set; }

        public static MevzuatGrafigiSonuc Tamam() => new() { Basarili = true };
        public static MevzuatGrafigiSonuc Hata(string mesaj) => new() { Mesaj = mesaj };
    }

    public class MevzuatGrafigiSonuc<T> : MevzuatGrafigiSonuc
    {
        public T? Veri { get; set; }

        public static MevzuatGrafigiSonuc<T> Tamam(T veri) => new() { Basarili = true, Veri = veri };
        public new static MevzuatGrafigiSonuc<T> Hata(string mesaj) => new() { Mesaj = mesaj };
    }
}

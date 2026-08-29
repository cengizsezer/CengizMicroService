using CatalogService.Api.Features.Ajanlar.Domain;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace CatalogService.Api.Features.Ajanlar.Services
{
    /// <summary>
    /// Bellekteki ajan deposu.
    ///
    /// <b>Anahtar MakineId</b>, ConnectionId değil: "aynı makine iki kere bağlı
    /// görünmesin" kuralı ancak makine kimliği anahtar olduğunda dictionary'nin
    /// kendisi tarafından garanti edilir. ConnectionId'ye göre arama listeyi
    /// tarayarak yapılıyor; bu listede bir avuç kayıt oluyor (ofis başına bir
    /// makine), ikinci bir indeks tutmanın tutarsızlık riski kazancından büyük.
    ///
    /// <b>Zaman aşımı okuma anında süzülüyor</b>, arka plan servisiyle değil:
    /// ölü kaydı yalnız listeyi okuyan görüyor, dolayısıyla temizliği de okuma
    /// yapabilir. Ayrı bir <c>BackgroundService</c> aynı işi bir de zamanlayıcı
    /// ve kendi hata yönetimiyle yapardı.
    /// </summary>
    public class AjanDeposu : IAjanDeposu
    {
        private readonly ConcurrentDictionary<string, AjanKaydi> _makineyeGore = new(StringComparer.OrdinalIgnoreCase);
        private readonly IOptionsMonitor<AgentHubAyarlari> _ayarlar;
        private readonly TimeProvider _saat;

        public AjanDeposu(IOptionsMonitor<AgentHubAyarlari> ayarlar, TimeProvider saat)
        {
            _ayarlar = ayarlar;
            _saat = saat;
        }

        private TimeSpan ZamanAsimi => TimeSpan.FromSeconds(Math.Max(1, _ayarlar.CurrentValue.KalpAtisiZamanAsimiSaniye));

        public AjanKaydetmeSonucu Kaydet(AjanKaydi ajan)
        {
            var simdi = _saat.GetUtcNow();
            ajan.BaglantiZamani = simdi;
            ajan.SonKalpAtisi = simdi;

            AjanKaydi? dusurulen = null;
            _makineyeGore.AddOrUpdate(ajan.MakineId, ajan, (_, eski) =>
            {
                // Aynı bağlantı ikinci kez Kaydol çağırdıysa bu bir "düşürme" değil,
                // yalnızca bilgi tazeleme.
                if (!string.Equals(eski.ConnectionId, ajan.ConnectionId, StringComparison.Ordinal))
                    dusurulen = eski;
                return ajan;
            });

            return new AjanKaydetmeSonucu(ajan, dusurulen);
        }

        public AjanKaydi? Cikar(string connectionId)
        {
            var kayit = Bul(connectionId);
            if (kayit is null) return null;

            // Karşılaştırmalı çıkarma: araya yeni bir kayıt girdiyse ona dokunma.
            return _makineyeGore.TryRemove(new KeyValuePair<string, AjanKaydi>(kayit.MakineId, kayit))
                ? kayit
                : null;
        }

        public bool KalpAtisi(string connectionId)
        {
            var kayit = Bul(connectionId);
            if (kayit is null) return false;
            kayit.SonKalpAtisi = _saat.GetUtcNow();
            return true;
        }

        public IReadOnlyList<AjanKaydi> Baglilar()
        {
            var simdi = _saat.GetUtcNow();
            var esik = simdi - ZamanAsimi;
            var canli = new List<AjanKaydi>();

            foreach (var kayit in _makineyeGore.Values)
            {
                if (kayit.SonKalpAtisi < esik)
                    _makineyeGore.TryRemove(new KeyValuePair<string, AjanKaydi>(kayit.MakineId, kayit));
                else
                    canli.Add(kayit);
            }

            return canli.OrderBy(a => a.MakineAdi, StringComparer.CurrentCultureIgnoreCase).ToList();
        }

        private AjanKaydi? Bul(string connectionId) =>
            _makineyeGore.Values.FirstOrDefault(a =>
                string.Equals(a.ConnectionId, connectionId, StringComparison.Ordinal));
    }
}

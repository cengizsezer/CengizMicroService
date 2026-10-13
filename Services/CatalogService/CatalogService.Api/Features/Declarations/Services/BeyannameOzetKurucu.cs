using CatalogService.Api.Features.Declarations.Dtos;
using CatalogService.Api.Features.Declarations.Entities;

namespace CatalogService.Api.Features.Declarations.Services
{
    /// <summary>
    /// Firma × beyanname türü matrisini kurar. <b>Saf fonksiyon</b>: veritabanı bilmez,
    /// listeleri alır matrisi verir — böylece matris kuralları (durum sırası, toplamlar,
    /// eşleşmeyen tür raporu) veritabanı kurmadan sınanabiliyor.
    ///
    /// Satırlar Firmalarım firmaları (<see cref="BeyannameFirmasi"/>; hangi firmaların geleceğine — aktif / pasif —
    /// servis karar verir). FirmaId'si boş (VKN ile eşleşmemiş eski) kayıt varsa sonda bir "Eşleşmeyen firma" satırı
    /// açılır: kayıt matristen sessizce düşmez.
    /// </summary>
    public static class BeyannameOzetKurucu
    {
        /// <summary>FirmaId'si boş kayıtların kova anahtarı (gerçek firma Id'si 0 olmaz).</summary>
        private const int EslesmeyenAnahtar = 0;

        public static BeyannameOzetDto Kur(
            int yil,
            int ay,
            IReadOnlyList<BeyannameTuru> turler,
            IReadOnlyList<BeyannameFirmasi> firmalar,
            IReadOnlyList<Declaration> beyannameler,
            IReadOnlyList<BeyannameEk> ekler)
        {
            var ozet = new BeyannameOzetDto { Yil = yil, Ay = ay };

            var sirali = turler.OrderBy(t => t.Sira).ThenBy(t => t.Id).ToList();
            ozet.Turler = sirali.Select(TuruDto).ToList();

            // Beyannameleri (firma, tür) çiftine dağıt. Tür eşleşmeyen kayıt sessizce
            // düşmez: metni raporlanır ve kullanıcı tanım tablosuna ekleyebilir.
            var kovalar = new Dictionary<(int FirmaId, int TuruId), List<Declaration>>();
            var eslesmeyen = new List<string>();

            foreach (var beyanname in beyannameler)
            {
                var tur = BeyannameTuruEsleyici.Esle(sirali, beyanname.DeclarationType);
                if (tur is null)
                {
                    var metin = (beyanname.DeclarationType ?? string.Empty).Trim();
                    if (metin.Length > 0 && !eslesmeyen.Contains(metin, StringComparer.OrdinalIgnoreCase))
                        eslesmeyen.Add(metin);
                    continue;
                }

                // FirmaId'si boş kayıt 0 anahtarında (Eşleşmeyen firma satırı); gerçek firma Id'si hiç 0 olmaz.
                var anahtar = (beyanname.FirmaId ?? EslesmeyenAnahtar, tur.Id);
                if (!kovalar.TryGetValue(anahtar, out var liste))
                    kovalar[anahtar] = liste = new List<Declaration>();

                liste.Add(beyanname);
            }

            ozet.EslesmeyenTurler = eslesmeyen;

            var eklerBeyannameye = ekler
                .GroupBy(e => e.DeclarationId)
                .ToDictionary(g => g.Key, g => g.ToList());

            var satirFirmalari = firmalar
                .Select(f => (Anahtar: f.Id, Satir: new BeyannameOzetSatirDto
                {
                    FirmaId = f.Id,
                    FirmaAdi = f.Ad,
                    VergiKimlikNo = f.VergiKimlikNo,
                    FirmaPasif = !f.Aktif
                }))
                .ToList();

            if (kovalar.Keys.Any(k => k.FirmaId == EslesmeyenAnahtar))
            {
                satirFirmalari.Add((EslesmeyenAnahtar, new BeyannameOzetSatirDto
                {
                    FirmaId = null,
                    FirmaAdi = BeyannameFirmasi.EslesmeyenAdi,
                    Eslesmeyen = true
                }));
            }

            var sira = 0;
            foreach (var (anahtarFirma, satir) in satirFirmalari)
            {
                satir.Sira = ++sira;

                foreach (var tur in sirali)
                {
                    kovalar.TryGetValue((anahtarFirma, tur.Id), out var kayitlar);
                    satir.Hucreler.Add(Hucre(tur, kayitlar, eklerBeyannameye));
                }

                satir.DoluHucreSayisi = satir.Hucreler.Count(h => h.Durum != BeyannameHucreDurum.Yok);
                satir.ToplamTutar = satir.Hucreler.Sum(h => h.Tutar);

                ozet.Satirlar.Add(satir);
            }

            foreach (var tur in sirali)
            {
                var kolon = ozet.Satirlar
                    .Select(s => s.Hucreler.First(h => h.TuruId == tur.Id))
                    .ToList();

                ozet.KolonToplamlari.Add(new BeyannameOzetKolonToplamDto
                {
                    TuruId = tur.Id,
                    DoluHucreSayisi = kolon.Count(h => h.Durum != BeyannameHucreDurum.Yok),
                    ToplamTutar = kolon.Sum(h => h.Tutar)
                });
            }

            ozet.ToplamBeyanname = ozet.Satirlar.Sum(s => s.DoluHucreSayisi);
            ozet.ToplamTutar = ozet.Satirlar.Sum(s => s.ToplamTutar);

            return ozet;
        }

        private static BeyannameOzetHucreDto Hucre(
            BeyannameTuru tur,
            List<Declaration>? kayitlar,
            Dictionary<int, List<BeyannameEk>> eklerBeyannameye)
        {
            var hucre = new BeyannameOzetHucreDto { TuruId = tur.Id };

            if (kayitlar is null || kayitlar.Count == 0)
            {
                hucre.Durum = BeyannameHucreDurum.Yok;
                return hucre;
            }

            var siraliKayitlar = kayitlar.OrderBy(k => k.Id).ToList();

            hucre.DeclarationId = siraliKayitlar[0].Id;
            hucre.KayitSayisi = siraliKayitlar.Count;
            hucre.Tutar = siraliKayitlar.Sum(k => k.Amount);

            // Aynı hücrede birden fazla kayıt varsa EN GERİ durum gösterilir: biri ödendi
            // diye hücre yeşile dönerse, yanındaki ödenmemiş kayıt görünmez olurdu.
            hucre.Durum = siraliKayitlar.Select(Durum).Min();

            foreach (var kayit in siraliKayitlar)
            {
                hucre.Kayitlar.Add(KayitDto(kayit));

                if (!eklerBeyannameye.TryGetValue(kayit.Id, out var ekler)) continue;

                foreach (var ek in ekler.OrderBy(e => e.Tur))
                    hucre.Ekler.Add(new BeyannameEkTuruDto { EkId = ek.Id, Tur = ek.Tur });
            }

            return hucre;
        }

        /// <summary>
        /// Kaydın matristeki durumu. Ödeme durumu beyanname durumunu <b>ezer</b>: ödenmiş
        /// bir kayıt, beyanname durumu ne olursa olsun kapanmıştır.
        /// </summary>
        public static BeyannameHucreDurum Durum(Declaration beyanname)
        {
            if (beyanname.PaymentStatus == PaymentStatus.Paid) return BeyannameHucreDurum.Odendi;

            return beyanname.DeclarationStatus >= DeclarationStatus.Approved
                ? BeyannameHucreDurum.Onaylandi
                : BeyannameHucreDurum.Hazirlandi;
        }

        private static DeclarationDto KayitDto(Declaration d) => new()
        {
            Id = d.Id,
            TenantNo = d.TenantNo,
            CompanyName = d.CompanyName,
            DeclarationType = d.DeclarationType,
            Year = d.Year,
            Month = d.Month,
            Amount = d.Amount,
            DueDate = d.DueDate,
            DeclarationStatus = d.DeclarationStatus,
            PaymentStatus = d.PaymentStatus,
            PaymentDate = d.PaymentDate,
            Note = d.Note,
            CustomerCompanyId = d.CustomerCompanyId,
            FirmaId = d.FirmaId
        };

        public static BeyannameTuruDto TuruDto(BeyannameTuru tur) => new()
        {
            Id = tur.Id,
            Deger = tur.Deger,
            Kod = tur.Kod,
            Ad = tur.Ad,
            Sira = tur.Sira,
            Aktif = tur.Aktif
        };
    }
}

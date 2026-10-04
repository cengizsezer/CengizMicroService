using CatalogService.Api.Features.Yapilacaklar.Domain;
using CatalogService.Api.Features.Yapilacaklar.Dtos;

namespace CatalogService.Api.Features.Yapilacaklar.Services
{
    /// <summary>
    /// Dönem panosu: <see cref="FirmaIsi"/> + <see cref="VergiTakvimi"/> + <see cref="IsTamamlama"/>'nın
    /// DÖNEME göre çevrilmiş hali. Yeni çekirdek tablo yok; saf fonksiyon, veritabanı ve saat yok.
    ///
    /// Yapılacaklar VADE merkezli ("ne zaman son gün"), bu ekran DÖNEM merkezli ("Ağustos kapandı
    /// mı"): Ağustos'un KDV'si 28 Eylül'de verilir ama Ağustos panosunda durur.
    ///
    /// <b>Bir iş bu ayda hangi dönemiyle görünür?</b> Ayda kapanan dönemiyle
    /// (<see cref="IsDonemi.AydaKapananlar"/>): aylık iş her ay, üç aylık iş mart/haziran/eylül/
    /// aralıkta, yıllık iş aralıkta. Geçerlilik kuralları kuyrukla AYNI:
    /// <list type="bullet">
    /// <item>yasal: firmanın mükellefiyet kodu × o dönemin takvim satırı. Satırın Aktif bayrağına
    /// BAKILMAZ (Prompt 10B): pasif bayrağı Yapılacaklar kuyruğunu temiz tutmak içindir, geçmiş
    /// dönem görünümünü kısıtlamaz — Temmuz'un KDV'si pasif diye "yok" görünmez;</item>
    /// <item>özel: ilk dönemi <see cref="YapilacaklarKurucu.OzelIlkDonem"/> (başlangıç ayı ya da
    /// eklendiği günün dönemi) — öncesi "bu firmada yok"; pasif iş yalnız işaretli dönemde görünür;</item>
    /// <item>tek seferlik iş: tarihinin ayında.</item>
    /// </list>
    /// </summary>
    public static class DonemPanosuKurucu
    {
        /// <param name="Prosedur">Özel işte işin kendisi; yasalda firmanın o işe açtığı prosedür satırı (yoksa boş).</param>
        private sealed record Hucre(string Kolon, string Baslik, IsKaynagi Tip, IsTekrari Tekrar, string? Kod,
                                    int KaynakId, string Anahtar, IsTamamlama? Tamam, DateTime? SonGun, FirmaIsi? Prosedur);

        /// <param name="ekSayilari">Tamamlama Id → dönem eki sayısı.</param>
        /// <param name="sistemAdlari">Sistem Id → ad (satır listesinin program çipi).</param>
        public static DonemPanosuDto Kur(int yil, int ay,
                                         IEnumerable<YapilacaklarKurucu.FirmaGirdisi> firmalar,
                                         IEnumerable<VergiTakvimi> takvim,
                                         IEnumerable<FirmaIsi> ozelIsler,
                                         IEnumerable<IsTamamlama> tamamlamalar,
                                         IReadOnlyDictionary<long, int>? ekSayilari = null,
                                         IReadOnlyDictionary<int, string>? sistemAdlari = null)
        {
            ekSayilari ??= new Dictionary<long, int>();
            sistemAdlari ??= new Dictionary<int, string>();
            var donemler = IsDonemi.AydaKapananlar(yil, ay).ToList();
            var tamamlar = tamamlamalar.ToDictionary(t => (t.KaynakTip, t.KaynakId, t.FirmaId, t.DonemAnahtari));

            // Bu ayda kapanan takvim satırları (seri başına bir satır).
            var takvimSatirlari = takvim
                .Where(t => donemler.Contains(new IsDonemi(t.Tekrar, t.Yil, t.DonemNo)))
                .ToList();

            var ozelByFirma = ozelIsler.Where(i => i.YasalMukellefiyetKodu is null).ToLookup(i => i.FirmaId);

            // Yasal işin prosedür satırı (Prompt 8): program ve alıcılar buradan.
            var prosedurler = ozelIsler.Where(i => i.YasalMukellefiyetKodu is not null)
                .GroupBy(i => (i.FirmaId, Kod: i.YasalMukellefiyetKodu!, i.Tekrar))
                .ToDictionary(g => g.Key, g => g.First());

            var firmaHucreleri = new List<(YapilacaklarKurucu.FirmaGirdisi Firma, Dictionary<string, Hucre> Hucreler)>();

            foreach (var firma in firmalar)
            {
                var hucreler = new Dictionary<string, Hucre>();

                // ---- Yasal ----
                foreach (var t in takvimSatirlari.Where(t => firma.Kodlar.Contains(t.MukellefiyetKodu))
                                                  .OrderBy(t => t.SonGun).ThenBy(t => t.Id))
                {
                    var anahtar = new IsDonemi(t.Tekrar, t.Yil, t.DonemNo).Anahtar;
                    tamamlar.TryGetValue((IsKaynagi.Yasal, t.Id, firma.FirmaId, anahtar), out var tamam);

                    // Takvim satırının Aktif bayrağı BURADA DİKKATE ALINMAZ (Prompt 10B). Pasif
                    // bayrağı kuyruğu temiz tutmak içindir; geçmiş dönem görünümünü kısıtlamaz.
                    // Yapılacaklar'da pasif satır hâlâ iş üretmez — iki ekran aynı veriyi farklı
                    // amaçla okuyor, kuralları ayrı.

                    var kolon = $"Y|{t.MukellefiyetKodu}|{(byte)t.Tekrar}";
                    prosedurler.TryGetValue((firma.FirmaId, t.MukellefiyetKodu, t.Tekrar), out var prosedur);
                    hucreler.TryAdd(kolon, new Hucre(kolon, t.Ad, IsKaynagi.Yasal, t.Tekrar, t.MukellefiyetKodu, t.Id, anahtar, tamam,
                                                     t.SonGun, prosedur));
                }

                // ---- Firmaya özel ----
                foreach (var isi in ozelByFirma[firma.FirmaId].OrderBy(i => i.Id))
                {
                    string anahtar;
                    DateTime sonGun;
                    if (isi.Tekrar == IsTekrari.TekSefer)
                    {
                        if (isi.TekSeferTarih is not { } t || t.Year != yil || t.Month != ay) continue;
                        anahtar = IsDonemi.TekSeferAnahtari;
                        sonGun = t.Date;
                    }
                    else
                    {
                        var donem = donemler.FirstOrDefault(d => d.Tekrar == isi.Tekrar);
                        if (donem == default) continue;                                         // bu ay kapanmıyor
                        if (donem.Bas < YapilacaklarKurucu.OzelIlkDonem(isi).Bas) continue;     // iş henüz yoktu
                        anahtar = donem.Anahtar;
                        sonGun = IsTakvimHesabi.SonGun(isi.GunKurali, isi.AyinGunu, donem);
                    }

                    tamamlar.TryGetValue((IsKaynagi.Ozel, isi.Id, firma.FirmaId, anahtar), out var tamam);
                    if (!isi.Aktif && tamam is not { Yapildi: true }) continue;   // pasif iş: yalnız yapılmış dönemi

                    var kolon = $"O|{isi.Baslik.Trim().ToUpper(IsTakvimHesabi.Tr)}|{(byte)isi.Tekrar}";
                    hucreler.TryAdd(kolon, new Hucre(kolon, isi.Baslik.Trim(), IsKaynagi.Ozel, isi.Tekrar, null, isi.Id, anahtar, tamam,
                                                     sonGun, isi));
                }

                firmaHucreleri.Add((firma, hucreler));
            }

            // Kolonlar: en az bir firmada geçerli olanlar; boş kolon çizilmez. Yasal önce (koda göre), sonra özel.
            var tr = StringComparer.Create(IsTakvimHesabi.Tr, true);
            var kolonlar = firmaHucreleri
                .SelectMany(f => f.Hucreler.Values)
                .GroupBy(h => h.Kolon)
                .Select(g => g.First())
                .OrderBy(h => h.Tip)
                .ThenBy(h => h.Kod)
                .ThenBy(h => h.Tekrar)
                .ThenBy(h => h.Baslik, tr)
                .Select(h => new DonemPanosuKolonDto
                {
                    Anahtar = h.Kolon, Baslik = h.Baslik, KaynakTip = h.Tip, Tekrar = h.Tekrar, MukellefiyetKodu = h.Kod
                })
                .ToList();

            var dto = new DonemPanosuDto
            {
                Yil = yil,
                Ay = ay,
                Etiket = new IsDonemi(IsTekrari.Aylik, yil, ay).Etiket,
                Kolonlar = kolonlar
            };

            foreach (var (firma, hucreler) in firmaHucreleri.OrderBy(f => f.Firma.Ad, tr))
            {
                var satir = new DonemPanosuSatirDto { FirmaId = firma.FirmaId, FirmaAdi = firma.Ad };

                foreach (var kolon in kolonlar)
                {
                    if (!hucreler.TryGetValue(kolon.Anahtar, out var h))
                    {
                        satir.Hucreler.Add(new DonemPanosuHucreDto { Durum = DonemHucreDurumu.Yok });
                        continue;
                    }

                    // Yapıldı = zaman dolu. Notu/kanıtı olan yapılmamış dönem EKSİK sayılır.
                    satir.Hucreler.Add(new DonemPanosuHucreDto
                    {
                        Durum = h.Tamam is { Yapildi: true } ? DonemHucreDurumu.Yapildi : DonemHucreDurumu.Eksik,
                        KaynakTip = h.Tip,
                        KaynakId = h.KaynakId,
                        DonemAnahtari = h.Anahtar,
                        EkSayisi = h.Tamam is null ? 0 : ekSayilari.GetValueOrDefault(h.Tamam.Id),
                        NotVar = !string.IsNullOrWhiteSpace(h.Tamam?.Not)
                    });
                }

                satir.Gecerli = satir.Hucreler.Count(c => c.Durum != DonemHucreDurumu.Yok);
                satir.Yapilan = satir.Hucreler.Count(c => c.Durum == DonemHucreDurumu.Yapildi);
                dto.Satirlar.Add(satir);

                // Satır listesi (Prompt 14): matrisin AYNI hücreleri, firma × iş. Sıra firma adı + kolon
                // sırasıdır, durumdan BAĞIMSIZ — işaretlenen satır yerinden zıplamaz.
                for (var i = 0; i < kolonlar.Count; i++)
                {
                    if (!hucreler.TryGetValue(kolonlar[i].Anahtar, out var h)) continue;
                    var c = satir.Hucreler[i];
                    var alicilar = h.Prosedur?.Alicilar.OrderBy(a => a.Sira).ThenBy(a => a.Id).ToList() ?? new();
                    dto.Isler.Add(new DonemIsSatiriDto
                    {
                        FirmaId = firma.FirmaId,
                        FirmaAdi = firma.Ad,
                        KolonAnahtari = kolonlar[i].Anahtar,
                        Baslik = h.Baslik,
                        KaynakTip = h.Tip,
                        KaynakId = h.KaynakId,
                        DonemAnahtari = h.Anahtar,
                        MukellefiyetKodu = h.Kod,
                        Tekrar = h.Tekrar,
                        Durum = c.Durum,
                        EkSayisi = c.EkSayisi,
                        NotVar = c.NotVar,
                        SonGun = h.SonGun,
                        SistemId = h.Prosedur?.SistemId,
                        SistemAdi = h.Prosedur?.SistemId is { } sid ? sistemAdlari.GetValueOrDefault(sid) : null,
                        Kime = alicilar.Where(a => a.AliciTipi == AliciTipi.Kime).Select(a => a.AdSoyad).ToList(),
                        Bilgi = alicilar.Where(a => a.AliciTipi == AliciTipi.Bilgi).Select(a => a.AdSoyad).ToList(),
                        IlkDonem = h.Tip == IsKaynagi.Ozel ? h.Prosedur?.IlkDonem : null
                    });
                }
            }

            // Üst şerit: matristeki hücrelerden sayılır — iki ayrı hesap tutmasın diye.
            dto.Toplam = dto.Satirlar.Sum(s => s.Gecerli);
            dto.Yapilan = dto.Satirlar.Sum(s => s.Yapilan);
            dto.Eksikler = kolonlar
                .Select((k, i) => new DonemEksikOzetiDto
                {
                    Baslik = k.Baslik,
                    FirmaSayisi = dto.Satirlar.Count(s => s.Hucreler[i].Durum == DonemHucreDurumu.Eksik)
                })
                .Where(e => e.FirmaSayisi > 0)
                .OrderByDescending(e => e.FirmaSayisi)
                .ThenBy(e => e.Baslik, tr)
                .ToList();

            return dto;
        }
    }
}

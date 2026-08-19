using CatalogService.Api.Features.BankaEkstre.Domain;
using CatalogService.Api.Features.BankaEkstre.Dtos;
using CatalogService.Api.Features.BankaEkstre.Services.Parsing;
using CatalogService.Api.Infrastructure.Auth;
using CatalogService.Api.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace CatalogService.Api.Features.BankaEkstre.Services
{
    public interface IEkstreService
    {
        Task<EkstreYuklemeDto> YukleAsync(int bankaHesabiId, Stream dosya, string dosyaAdi, CancellationToken ct = default);
        Task<List<EkstreYuklemeDto>> GetYuklemelerAsync(CancellationToken ct = default);
        Task<EkstreYuklemeDto?> GetYuklemeAsync(int id, CancellationToken ct = default);
        Task<List<EkstreSatirDto>?> GetSatirlarAsync(int ekstreId, SatirDurum? durum, CancellationToken ct = default);
        Task<EkstreSatirDto?> OnaylaAsync(int satirId, string hesapKodu, CancellationToken ct = default);
        Task<EkstreSatirDto?> DigerBankadaAsync(int satirId, CancellationToken ct = default);
        Task<DisaAktarimSonucDto?> DisaAktarAsync(int ekstreId, CancellationToken ct = default);
        Task<bool> SilAsync(int ekstreId, CancellationToken ct = default);
    }

    /// <summary>
    /// Ekstre yükleme, satır işleme, onay ve dışa aktarım. Yükleme anında her satır için
    /// açıklama üretilir ve karşı hesap katmanlı olarak çözülür; belirsiz kalan satırlar
    /// onaya düşer. Onaylar <see cref="OgrenmeKaydi"/> tablosuna yazılır ve bir sonraki
    /// yüklemede Katman 1/2'den çözülür.
    /// </summary>
    public class EkstreService : IEkstreService
    {
        private readonly CatalogContext _db;
        private readonly IEkstreParserSecici _parserSecici;
        private readonly IUnvanCikarici _unvanCikarici;
        private readonly IAciklamaUretici _aciklamaUretici;
        private readonly IHesapEslestirici _eslestirici;
        private readonly IHttpCurrentUser _kullanici;

        public EkstreService(
            CatalogContext db,
            IEkstreParserSecici parserSecici,
            IUnvanCikarici unvanCikarici,
            IAciklamaUretici aciklamaUretici,
            IHesapEslestirici eslestirici,
            IHttpCurrentUser kullanici)
        {
            _db = db;
            _parserSecici = parserSecici;
            _unvanCikarici = unvanCikarici;
            _aciklamaUretici = aciklamaUretici;
            _eslestirici = eslestirici;
            _kullanici = kullanici;
        }

        // ---- Yükleme ve işleme ----

        public async Task<EkstreYuklemeDto> YukleAsync(int bankaHesabiId, Stream dosya, string dosyaAdi, CancellationToken ct = default)
        {
            var hesap = await _db.EkstreBankaHesaplari.FirstOrDefaultAsync(h => h.Id == bankaHesabiId, ct)
                        ?? throw new BankaEkstreKuralException(nameof(bankaHesabiId), "Banka hesabı bulunamadı.");

            var parser = _parserSecici.Sec(hesap.ParserTipi)
                         ?? throw new BankaEkstreKuralException(nameof(hesap.ParserTipi),
                             $"'{hesap.ParserTipi}' için ayrıştırıcı tanımlı değil.");

            var ayristirma = parser.Ayristir(dosya);

            var yukleme = new EkstreYukleme
            {
                BankaHesabiId = hesap.Id,
                DosyaAdi = Normalizasyon.Kirp(dosyaAdi, 260),
                YuklemeTarihi = DateTime.Now,
                DonemBaslangic = ayristirma.DonemBaslangic,
                DonemBitis = ayristirma.DonemBitis,
                SatirSayisi = ayristirma.Satirlar.Count,
                Durum = ayristirma.Satirlar.Count == 0 ? YuklemeDurum.Hatali : YuklemeDurum.Tamamlandi,
                Uyarilar = ayristirma.Uyarilar.Count == 0 ? null : string.Join(Environment.NewLine, ayristirma.Uyarilar)
            };

            if (ayristirma.AtlananSatir > 0)
                yukleme.Uyarilar = (yukleme.Uyarilar is null ? string.Empty : yukleme.Uyarilar + Environment.NewLine)
                                   + $"{ayristirma.AtlananSatir} satır tarih/tutar okunamadığı için atlandı.";

            _db.EkstreYuklemeler.Add(yukleme);
            await _db.SaveChangesAsync(ct);

            var veri = await EslestirmeVerisiYukleAsync(hesap.Id, ct);
            var sablonlar = await SablonlariYukleAsync(hesap.ParserTipi, ct);
            var desenler = await DesenleriYukleAsync(hesap.ParserTipi, ct);

            foreach (var ayrilan in ayristirma.Satirlar)
            {
                var satir = SatirOlustur(yukleme.Id, ayrilan, sablonlar, desenler, veri);
                _db.EkstreSatirlari.Add(satir);
            }

            await _db.SaveChangesAsync(ct);

            return (await GetYuklemeAsync(yukleme.Id, ct))!;
        }

        /// <summary>Tek satırın açıklama üretimi + katmanlı eşleştirmesi.</summary>
        private EkstreSatiri SatirOlustur(
            int yuklemeId,
            AyrilanSatir ayrilan,
            IReadOnlyList<AciklamaSablonu> sablonlar,
            IReadOnlyList<UnvanDeseni> desenler,
            EslestirmeVerisi veri)
        {
            var baglam = new SatirBaglami
            {
                IslemTipi = ayrilan.IslemTipi,
                HamAciklama = ayrilan.HamAciklama,
                Yon = ayrilan.Yon,
                KarsiIban = ayrilan.KarsiIban,
                KarsiVkn = ayrilan.KarsiVkn,
                Unvan = _unvanCikarici.Cikar(ayrilan.HamAciklama, desenler)
            };

            baglam.Sablon = _aciklamaUretici.SablonBul(ayrilan.IslemTipi, sablonlar);

            // Bankalar arası hareketlerde açıklamada unvan yerine banka adı geçer;
            // bu yüzden banka, açıklama üretiminden önce bulunur.
            baglam.BankaAdi = _eslestirici.BankaBul(baglam, veri)?.BankaAdi;

            var aciklama = _aciklamaUretici.Uret(baglam);
            var eslestirme = _eslestirici.Coz(baglam, veri);

            return new EkstreSatiri
            {
                EkstreYuklemeId = yuklemeId,
                SiraNo = ayrilan.SiraNo,
                Tarih = ayrilan.Tarih,
                Yon = ayrilan.Yon,
                Tutar = ayrilan.Tutar,
                IslemTipi = Normalizasyon.Kirp(ayrilan.IslemTipi, 150),
                HamAciklama = ayrilan.HamAciklama,
                KarsiIban = ayrilan.KarsiIban,
                KarsiVkn = ayrilan.KarsiVkn,
                Kanal = Normalizasyon.Kirp(ayrilan.Kanal, 100) is { Length: > 0 } k ? k : null,
                UretilenAciklama = aciklama,
                CikarilanUnvan = baglam.Unvan,
                OnerilenHesapKodu = eslestirme.HesapKodu,
                OnerilenHesapAdi = eslestirme.HesapAdi,
                GuvenSkoru = Math.Round(eslestirme.Guven, 4),
                KaynakKatman = eslestirme.Katman,
                IkinciAdayKodu = eslestirme.IkinciAdayKodu,
                IkinciAdayAdi = eslestirme.IkinciAdayAdi,
                IkinciAdaySkoru = eslestirme.IkinciAdaySkoru is decimal s ? Math.Round(s, 4) : null,
                Durum = eslestirme.Durum
            };
        }

        // ---- Listeleme ----

        public async Task<List<EkstreYuklemeDto>> GetYuklemelerAsync(CancellationToken ct = default)
        {
            var yuklemeler = await _db.EkstreYuklemeler.AsNoTracking()
                .Include(y => y.BankaHesabi)
                .OrderByDescending(y => y.Id)
                .ToListAsync(ct);

            if (yuklemeler.Count == 0) return new();

            var idler = yuklemeler.Select(y => y.Id).ToList();
            var sayaclar = await SayaclariYukleAsync(idler, ct);

            return yuklemeler
                .Select(y => Esle(y, sayaclar.TryGetValue(y.Id, out var s) ? s : new EkstreSayaclariDto()))
                .ToList();
        }

        public async Task<EkstreYuklemeDto?> GetYuklemeAsync(int id, CancellationToken ct = default)
        {
            var yukleme = await _db.EkstreYuklemeler.AsNoTracking()
                .Include(y => y.BankaHesabi)
                .FirstOrDefaultAsync(y => y.Id == id, ct);

            if (yukleme is null) return null;

            var sayaclar = await SayaclariYukleAsync(new[] { id }, ct);
            return Esle(yukleme, sayaclar.TryGetValue(id, out var s) ? s : new EkstreSayaclariDto());
        }

        public async Task<List<EkstreSatirDto>?> GetSatirlarAsync(int ekstreId, SatirDurum? durum, CancellationToken ct = default)
        {
            if (!await _db.EkstreYuklemeler.AnyAsync(y => y.Id == ekstreId, ct)) return null;

            var sorgu = _db.EkstreSatirlari.AsNoTracking().Where(s => s.EkstreYuklemeId == ekstreId);
            if (durum is SatirDurum d) sorgu = sorgu.Where(s => s.Durum == d);

            var satirlar = await sorgu.OrderBy(s => s.SiraNo).ToListAsync(ct);
            return satirlar.Select(Esle).ToList();
        }

        // ---- Onay ve öğrenme ----

        /// <summary>
        /// Satırı onaylar ve öğrenme kayıtlarını yazar. Kullanıcı önerilenden farklı bir
        /// kod seçtiyse eski öğrenme kaydı ezilir — düzeltme her zaman kazanır.
        /// </summary>
        public async Task<EkstreSatirDto?> OnaylaAsync(int satirId, string hesapKodu, CancellationToken ct = default)
        {
            var satir = await SatirGetirAsync(satirId, ct);
            if (satir is null) return null;

            var kod = Normalizasyon.HesapKoduNormalize(hesapKodu);
            if (kod.Length == 0)
                throw new BankaEkstreKuralException(nameof(hesapKodu), "Hesap kodu boş olamaz.");

            var plan = await _db.EkstreHesapPlani.AsNoTracking().FirstOrDefaultAsync(h => h.Kod == kod, ct);

            // Hesap planı henüz içe aktarılmadıysa onay engellenmez; ad boş kalır.
            // Ama plan doluysa bilinmeyen kod kabul edilmez — kod uydurulmasın.
            if (plan is null && await _db.EkstreHesapPlani.AnyAsync(ct))
                throw new BankaEkstreKuralException(nameof(hesapKodu),
                    $"'{kod}' hesap planında yok. Kodu kontrol edin veya hesap planını yeniden içe aktarın.");

            satir.OnaylananHesapKodu = kod;
            satir.OnaylananHesapAdi = plan?.Ad;
            satir.OnayTarihi = DateTime.Now;
            satir.OnaylayanKullanici = Normalizasyon.Kirp(_kullanici.UserName ?? _kullanici.UserId, 100);
            satir.Durum = SatirDurum.Onaylandi;
            satir.KaynakKatman = KaynakKatman.Kullanici;

            await OgrenAsync(satir, kod, plan?.Ad, ct);
            await _db.SaveChangesAsync(ct);

            return Esle(satir);
        }

        /// <summary>
        /// Bankalar arası transferin karşı bacağı başka bankanın ekstresinde işlendiyse
        /// satır dışa aktarımdan düşer. Öğrenme kaydı yazılmaz — bu bir hesap kararı değil.
        /// </summary>
        public async Task<EkstreSatirDto?> DigerBankadaAsync(int satirId, CancellationToken ct = default)
        {
            var satir = await SatirGetirAsync(satirId, ct);
            if (satir is null) return null;

            satir.Durum = SatirDurum.DigerBankada;
            satir.OnayTarihi = DateTime.Now;
            satir.OnaylayanKullanici = Normalizasyon.Kirp(_kullanici.UserName ?? _kullanici.UserId, 100);

            await _db.SaveChangesAsync(ct);
            return Esle(satir);
        }

        private async Task OgrenAsync(EkstreSatiri satir, string kod, string? ad, CancellationToken ct)
        {
            await AnahtarYazAsync(AnahtarTipi.AciklamaHash, Normalizasyon.AciklamaHash(satir.HamAciklama), satir.Yon, kod, ad, ct);
            await AnahtarYazAsync(AnahtarTipi.Iban, Normalizasyon.IbanAnahtar(satir.KarsiIban), satir.Yon, kod, ad, ct);
            await AnahtarYazAsync(AnahtarTipi.Vkn, Normalizasyon.VknAnahtar(satir.KarsiVkn), satir.Yon, kod, ad, ct);
        }

        private async Task AnahtarYazAsync(AnahtarTipi tip, string anahtar, Yon yon, string kod, string? ad, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(anahtar)) return;

            var mevcut = await _db.EkstreOgrenmeKayitlari
                .FirstOrDefaultAsync(o => o.AnahtarTipi == tip && o.Anahtar == anahtar && o.Yon == yon, ct);

            if (mevcut is null)
            {
                _db.EkstreOgrenmeKayitlari.Add(new OgrenmeKaydi
                {
                    AnahtarTipi = tip,
                    Anahtar = anahtar,
                    Yon = yon,
                    HesapKodu = kod,
                    HesapAdi = ad,
                    KullanimSayisi = 1,
                    SonKullanim = DateTime.Now
                });
                return;
            }

            // Farklı kod seçildiyse eski kayıt ezilir; sayaç yeniden başlar çünkü
            // eski koda ait kullanım artık bu anahtarı temsil etmiyor.
            if (!string.Equals(mevcut.HesapKodu, kod, StringComparison.Ordinal))
            {
                mevcut.HesapKodu = kod;
                mevcut.HesapAdi = ad;
                mevcut.KullanimSayisi = 1;
            }
            else
            {
                mevcut.KullanimSayisi++;
            }

            mevcut.SonKullanim = DateTime.Now;
        }

        // ---- Dışa aktarım ----

        /// <summary>
        /// ORKA çıktısı. Çözülemedi veya onay bekleyen satır varsa üretilmez:
        /// eksik listeyle ORKA'ya gitmenin anlamı yok.
        /// </summary>
        public async Task<DisaAktarimSonucDto?> DisaAktarAsync(int ekstreId, CancellationToken ct = default)
        {
            var yukleme = await _db.EkstreYuklemeler.AsNoTracking()
                .Include(y => y.BankaHesabi)
                .FirstOrDefaultAsync(y => y.Id == ekstreId, ct);

            if (yukleme is null) return null;

            var satirlar = await _db.EkstreSatirlari.AsNoTracking()
                .Where(s => s.EkstreYuklemeId == ekstreId)
                .OrderBy(s => s.SiraNo)
                .ToListAsync(ct);

            var eksik = satirlar.Count(s => s.Durum is SatirDurum.OnayBekliyor or SatirDurum.Cozulemedi);
            if (eksik > 0)
                throw new BankaEkstreKuralException("satirlar",
                    $"{eksik} satır hâlâ çözülmemiş (onay bekleyen veya çözülemeyen). " +
                    "Eksik listeyle dışa aktarım yapılmaz; önce onay ekranını tamamlayın.");

            var aktarilacak = satirlar.Where(s => s.Durum != SatirDurum.DigerBankada).ToList();
            var bankaKodu = yukleme.BankaHesabi?.OrkaHesapKodu ?? string.Empty;

            return new DisaAktarimSonucDto
            {
                EkstreId = ekstreId,
                DosyaAdi = yukleme.DosyaAdi,
                SatirSayisi = aktarilacak.Count,
                DigerBankadaAtlanan = satirlar.Count - aktarilacak.Count,
                Satirlar = aktarilacak.Select(s => new OrkaSatirDto
                {
                    SiraNo = s.SiraNo,
                    Tarih = s.Tarih,
                    Aciklama = s.UretilenAciklama ?? string.Empty,
                    Yon = s.Yon,
                    Tutar = s.Tutar,
                    HesapKodu = s.EtkinHesapKodu ?? string.Empty,
                    HesapAdi = s.OnaylananHesapAdi ?? s.OnerilenHesapAdi,
                    BankaHesapKodu = bankaKodu
                }).ToList()
            };
        }

        public async Task<bool> SilAsync(int ekstreId, CancellationToken ct = default)
        {
            var yukleme = await _db.EkstreYuklemeler.FirstOrDefaultAsync(y => y.Id == ekstreId, ct);
            if (yukleme is null) return false;

            // Satırlar cascade ile gider; öğrenilen kayıtlar kalır (bilgi kaybolmasın).
            _db.EkstreYuklemeler.Remove(yukleme);
            await _db.SaveChangesAsync(ct);
            return true;
        }

        // ---- Yardımcılar ----

        /// <summary>Satırı, bağlı olduğu yükleme tenant filtresinden geçtiği için güvenle getirir.</summary>
        private Task<EkstreSatiri?> SatirGetirAsync(int satirId, CancellationToken ct)
            => _db.EkstreSatirlari
                .Where(s => s.Id == satirId && _db.EkstreYuklemeler.Any(y => y.Id == s.EkstreYuklemeId))
                .FirstOrDefaultAsync(ct);

        private async Task<EslestirmeVerisi> EslestirmeVerisiYukleAsync(int islenenHesapId, CancellationToken ct)
            => new()
            {
                OgrenmeKayitlari = await _db.EkstreOgrenmeKayitlari.AsNoTracking().ToListAsync(ct),
                BankaHesaplari = await _db.EkstreBankaHesaplari.AsNoTracking().ToListAsync(ct),
                SabitKurallar = await _db.EkstreSabitKurallar.AsNoTracking().ToListAsync(ct),
                HesapPlani = await _db.EkstreHesapPlani.AsNoTracking().Where(h => h.Aktif).ToListAsync(ct),
                IslenenBankaHesabiId = islenenHesapId
            };

        private Task<List<AciklamaSablonu>> SablonlariYukleAsync(string parserTipi, CancellationToken ct)
            => _db.EkstreAciklamaSablonlari.AsNoTracking()
                .Where(s => s.ParserTipi == parserTipi && s.Aktif)
                .OrderBy(s => s.Sira)
                .ToListAsync(ct);

        private Task<List<UnvanDeseni>> DesenleriYukleAsync(string parserTipi, CancellationToken ct)
            => _db.EkstreUnvanDesenleri.AsNoTracking()
                .Where(d => d.ParserTipi == parserTipi && d.Aktif)
                .OrderBy(d => d.Sira)
                .ToListAsync(ct);

        private async Task<Dictionary<int, EkstreSayaclariDto>> SayaclariYukleAsync(IReadOnlyCollection<int> ekstreIdler, CancellationToken ct)
        {
            var ham = await _db.EkstreSatirlari.AsNoTracking()
                .Where(s => ekstreIdler.Contains(s.EkstreYuklemeId))
                .GroupBy(s => new { s.EkstreYuklemeId, s.Durum })
                .Select(g => new { g.Key.EkstreYuklemeId, g.Key.Durum, Adet = g.Count() })
                .ToListAsync(ct);

            var sonuc = new Dictionary<int, EkstreSayaclariDto>();
            foreach (var satir in ham)
            {
                if (!sonuc.TryGetValue(satir.EkstreYuklemeId, out var sayac))
                    sonuc[satir.EkstreYuklemeId] = sayac = new EkstreSayaclariDto();

                sayac.Toplam += satir.Adet;
                switch (satir.Durum)
                {
                    case SatirDurum.Otomatik: sayac.Otomatik += satir.Adet; break;
                    case SatirDurum.OnayBekliyor: sayac.OnayBekleyen += satir.Adet; break;
                    case SatirDurum.Onaylandi: sayac.Onaylanan += satir.Adet; break;
                    case SatirDurum.Cozulemedi: sayac.Cozulemeyen += satir.Adet; break;
                    case SatirDurum.DigerBankada: sayac.DigerBankada += satir.Adet; break;
                }
            }

            return sonuc;
        }

        private static EkstreYuklemeDto Esle(EkstreYukleme y, EkstreSayaclariDto sayaclar) => new()
        {
            Id = y.Id,
            BankaHesabiId = y.BankaHesabiId,
            BankaAdi = y.BankaHesabi?.BankaAdi ?? string.Empty,
            DosyaAdi = y.DosyaAdi,
            YuklemeTarihi = y.YuklemeTarihi,
            DonemBaslangic = y.DonemBaslangic,
            DonemBitis = y.DonemBitis,
            SatirSayisi = y.SatirSayisi,
            Durum = y.Durum,
            Uyarilar = y.Uyarilar,
            Sayaclar = sayaclar
        };

        private static EkstreSatirDto Esle(EkstreSatiri s) => new()
        {
            Id = s.Id,
            SiraNo = s.SiraNo,
            Tarih = s.Tarih,
            Yon = s.Yon,
            Tutar = s.Tutar,
            IslemTipi = s.IslemTipi,
            HamAciklama = s.HamAciklama,
            KarsiIban = s.KarsiIban,
            KarsiVkn = s.KarsiVkn,
            Kanal = s.Kanal,
            UretilenAciklama = s.UretilenAciklama,
            CikarilanUnvan = s.CikarilanUnvan,
            OnerilenHesapKodu = s.OnerilenHesapKodu,
            OnerilenHesapAdi = s.OnerilenHesapAdi,
            GuvenSkoru = s.GuvenSkoru,
            KaynakKatman = s.KaynakKatman,
            IkinciAdayKodu = s.IkinciAdayKodu,
            IkinciAdayAdi = s.IkinciAdayAdi,
            IkinciAdaySkoru = s.IkinciAdaySkoru,
            OnaylananHesapKodu = s.OnaylananHesapKodu,
            OnaylananHesapAdi = s.OnaylananHesapAdi,
            Durum = s.Durum
        };
    }
}

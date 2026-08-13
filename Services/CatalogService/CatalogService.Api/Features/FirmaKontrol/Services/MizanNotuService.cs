using CatalogService.Api.Features.FirmaKontrol.Domain;
using CatalogService.Api.Features.FirmaKontrol.Dtos;
using CatalogService.Api.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace CatalogService.Api.Features.FirmaKontrol.Services
{
    public class MizanNotuService : IMizanNotuService
    {
        /// <summary>MizanNotu.Metin sütununun genişliği — devir etiketi eklenirken taşma kontrolü için.</summary>
        private const int MetinMaxUzunluk = 2000;

        /// <summary>FirmaKontrolMizanSatir.Donem: 0=Onceki, 1=Cari. Snapshot cari dönemden alınır.</summary>
        private const int CariDonem = 1;

        private readonly CatalogContext _db;

        public MizanNotuService(CatalogContext db) => _db = db;

        public async Task<List<MizanNotuDto>> GetNotlarAsync(int firmaId, int? yil, CancellationToken ct = default)
        {
            // Kalıcı notlar her dönemde görünür; yıl verilmişse o yılın dönem notları eklenir.
            var notlar = await _db.MizanNotlari
                .AsNoTracking()
                .Where(n => n.FirmaId == firmaId &&
                            (yil == null || n.DonemYili == null || n.DonemYili == yil))
                .ToListAsync(ct);

            // AnaHesapKodu SQL'e çevrilemez — projeksiyon bellekte yapılır.
            // Sıra: kalıcı not (DonemYili null) önce, sonra hesap kodu.
            return notlar
                .OrderBy(n => MizanNotu.AnaHesapKodu(n.HesapKodu), StringComparer.Ordinal)
                .ThenBy(n => n.DonemYili.HasValue)
                .ThenBy(n => n.HesapKodu, StringComparer.Ordinal)
                .Select(MapToDto)
                .ToList();
        }

        public async Task<MizanNotuDto> UpsertAsync(int firmaId, MizanNotuUpsertDto dto, CancellationToken ct = default)
        {
            await EnsureFirmaExistsAsync(firmaId, ct);

            if (string.IsNullOrWhiteSpace(dto.HesapKodu))
                throw new ArgumentException("Hesap kodu boş olamaz.");

            if (string.IsNullOrWhiteSpace(dto.Metin))
                throw new ArgumentException("Not metni boş olamaz.");

            var hesapKodu = dto.HesapKodu.Trim();

            var entity = await _db.MizanNotlari
                .FirstOrDefaultAsync(n => n.FirmaId == firmaId &&
                                          n.HesapKodu == hesapKodu &&
                                          n.DonemYili == dto.DonemYili, ct);

            if (entity is null)
            {
                entity = new MizanNotu
                {
                    FirmaId = firmaId,
                    HesapKodu = hesapKodu,
                    DonemYili = dto.DonemYili,
                    CreatedAt = DateTime.UtcNow
                };
                _db.MizanNotlari.Add(entity);
            }
            else
            {
                entity.UpdatedAt = DateTime.UtcNow;
            }

            entity.Metin = Kirp(dto.Metin.Trim());
            entity.NotTuru = dto.NotTuru;
            entity.UyariBastir = dto.UyariBastir;

            // Not yazıldığı andaki bakiye — hem yeni kayıtta hem güncellemede tazelenir.
            await SnapshotAlAsync(entity, ct);

            await _db.SaveChangesAsync(ct);

            return MapToDto(entity);
        }

        public async Task<MizanNotuDto> GuncelleAsync(int firmaId, long id, MizanNotuGuncelleDto dto, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(dto.Metin))
                throw new ArgumentException("Not metni boş olamaz.");

            var entity = await _db.MizanNotlari
                .FirstOrDefaultAsync(n => n.Id == id && n.FirmaId == firmaId, ct);

            if (entity is null)
                throw new KeyNotFoundException($"Mizan notu bulunamadı: Id={id}");

            // Tip değişimi notun (FirmaId, HesapKodu, DonemYili) anahtarını değiştirir.
            // Hedef anahtar doluysa unique index'e çarpıp anlaşılmaz DB hatası vermek
            // yerine burada anlaşılır mesajla dururuz.
            if (entity.DonemYili != dto.DonemYili)
            {
                var cakisiyor = await _db.MizanNotlari.AnyAsync(
                    n => n.FirmaId == firmaId &&
                         n.HesapKodu == entity.HesapKodu &&
                         n.DonemYili == dto.DonemYili &&
                         n.Id != id, ct);

                if (cakisiyor)
                    throw new InvalidOperationException(dto.DonemYili.HasValue
                        ? $"{entity.HesapKodu} hesabının {dto.DonemYili} dönem notu zaten var. Önce onu silin."
                        : $"{entity.HesapKodu} hesabının kalıcı notu zaten var. Önce onu silin.");
            }

            entity.Metin = Kirp(dto.Metin.Trim());
            entity.NotTuru = dto.NotTuru;
            entity.DonemYili = dto.DonemYili;
            entity.UyariBastir = dto.UyariBastir;
            entity.UpdatedAt = DateTime.UtcNow;

            await SnapshotAlAsync(entity, ct);

            await _db.SaveChangesAsync(ct);

            return MapToDto(entity);
        }

        public async Task<MizanNotuDto> SnapshotYenileAsync(int firmaId, long id, CancellationToken ct = default)
        {
            var entity = await _db.MizanNotlari
                .FirstOrDefaultAsync(n => n.Id == id && n.FirmaId == firmaId, ct);

            if (entity is null)
                throw new KeyNotFoundException($"Mizan notu bulunamadı: Id={id}");

            var oncekiTarih = entity.SnapshotTarihi;
            await SnapshotAlAsync(entity, ct);

            // Snapshot yalnızca mizanda karşılık bulununca tazelenir. Tarih değişmediyse
            // hesap mizanda yok demektir — sessizce "güncellendi" demek yanıltıcı olur.
            if (entity.SnapshotTarihi == oncekiTarih)
                throw new InvalidOperationException(
                    $"{entity.HesapKodu} hesabı mizanda bulunamadı; mevcut snapshot korundu.");

            await _db.SaveChangesAsync(ct);

            return MapToDto(entity);
        }

        public async Task<bool> SilAsync(int firmaId, long id, CancellationToken ct = default)
        {
            var entity = await _db.MizanNotlari
                .FirstOrDefaultAsync(n => n.Id == id && n.FirmaId == firmaId, ct);

            if (entity is null) return false;

            _db.MizanNotlari.Remove(entity);
            await _db.SaveChangesAsync(ct);
            return true;
        }

        public async Task<List<MizanNotuDto>> DevirAdaylariAsync(int firmaId, int kaynakYil, int hedefYil, CancellationToken ct = default)
        {
            // Kalıcı notlar (DonemYili null) zaten her dönemde göründüğü için aday değildir.
            var kaynaklar = await _db.MizanNotlari
                .AsNoTracking()
                .Where(n => n.FirmaId == firmaId && n.DonemYili == kaynakYil)
                .ToListAsync(ct);

            if (kaynaklar.Count == 0) return new List<MizanNotuDto>();

            var hedeftekiKodlar = await HedefKodlariAsync(firmaId, hedefYil, ct);

            return kaynaklar
                .Where(n => !hedeftekiKodlar.Contains(n.HesapKodu))
                .OrderBy(n => n.HesapKodu, StringComparer.Ordinal)
                .Select(MapToDto)
                .ToList();
        }

        public async Task<List<MizanNotuDto>> DevretAsync(int firmaId, MizanNotuDevirRequest req, CancellationToken ct = default)
        {
            await EnsureFirmaExistsAsync(firmaId, ct);

            if (req.KaynakYil == req.HedefYil)
                throw new ArgumentException("Kaynak ve hedef dönem aynı olamaz.");

            if (req.NotIdleri.Count == 0)
                return new List<MizanNotuDto>();

            var secilenler = req.NotIdleri.Distinct().ToList();

            var kaynaklar = await _db.MizanNotlari
                .AsNoTracking()
                .Where(n => n.FirmaId == firmaId &&
                            n.DonemYili == req.KaynakYil &&
                            secilenler.Contains(n.Id))
                .ToListAsync(ct);

            if (kaynaklar.Count == 0) return new List<MizanNotuDto>();

            // Hedef yılda aynı hesap için not varsa üzerine yazmayız — devir eklemedir,
            // mevcut dönemin notunu ezmez (unique index de buna izin vermezdi).
            var hedeftekiKodlar = await HedefKodlariAsync(firmaId, req.HedefYil, ct);

            var etiket = DevirEtiketi(req.KaynakYil);
            var now = DateTime.UtcNow;
            var yeniler = new List<MizanNotu>();

            foreach (var kaynak in kaynaklar.OrderBy(n => n.HesapKodu, StringComparer.Ordinal))
            {
                if (!hedeftekiKodlar.Add(kaynak.HesapKodu)) continue;

                var yeni = new MizanNotu
                {
                    FirmaId = firmaId,
                    HesapKodu = kaynak.HesapKodu,
                    Metin = Kirp($"{kaynak.Metin} {etiket}"),
                    NotTuru = kaynak.NotTuru,
                    DonemYili = req.HedefYil,
                    UyariBastir = kaynak.UyariBastir,
                    CreatedAt = now
                };

                _db.MizanNotlari.Add(yeni);
                yeniler.Add(yeni);
            }

            if (yeniler.Count == 0) return new List<MizanNotuDto>();

            // Devir de not oluşturur — yeni dönemin notu güncel bakiyeyle işaretlenir.
            await SnapshotAlAsync(yeniler, ct);

            await _db.SaveChangesAsync(ct);

            return yeniler.Select(MapToDto).ToList();
        }

        /// <summary>
        /// Notun hesabının mizandaki güncel cari dönem değerini snapshot'a yazar.
        /// Karşılığı yoksa (alt kırılım kodu — mizan yalnızca 3 haneli ana hesapları
        /// saklar — ya da mizan henüz yüklenmemişse) alanlar null bırakılır: yanlış
        /// bir tutar yazmaktansa referans noktası olmasın.
        /// </summary>
        private Task SnapshotAlAsync(MizanNotu entity, CancellationToken ct) =>
            SnapshotAlAsync(new[] { entity }, ct);

        /// <summary>
        /// Birden çok not için snapshot — devirde N not için N sorgu atılmasın diye
        /// mizan satırları tek sorguda çekilir.
        /// </summary>
        private async Task SnapshotAlAsync(IReadOnlyCollection<MizanNotu> notlar, CancellationToken ct)
        {
            if (notlar.Count == 0) return;

            var firmaId = notlar.First().FirmaId;
            var kodlar = notlar.Select(n => n.HesapKodu).Distinct().ToList();

            var satirlar = await _db.FirmaKontrolMizanSatirlari
                .AsNoTracking()
                .Where(m => m.FirmaId == firmaId &&
                            m.Donem == CariDonem &&
                            kodlar.Contains(m.Kod))
                .ToListAsync(ct);

            // Ekranda görünen mizan en son yüklenen dönemdir; kod başına Yil'ce en büyük satır.
            var enGuncel = satirlar
                .GroupBy(m => m.Kod, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    g => g.Key,
                    g => g.OrderByDescending(m => m.Yil).First(),
                    StringComparer.OrdinalIgnoreCase);

            var now = DateTime.UtcNow;

            foreach (var not in notlar)
            {
                // Mizanda karşılık YOKSA mevcut snapshot olduğu gibi korunur, null'lanmaz:
                // öksüz notta en çok ihtiyaç duyulan bilgi "yazıldığında bakiyesi neydi".
                // Yalnızca karşılık bulununca üzerine yazılır.
                if (!enGuncel.TryGetValue(not.HesapKodu, out var satir)) continue;

                // Borç/Alacak mizan hattında saklanmadığından (bkz. MizanNotu.SnapshotBorc)
                // bu iki alan bugün her koşulda null kalır.
                not.SnapshotBorc = null;
                not.SnapshotAlacak = null;
                not.SnapshotBakiye = satir.Bakiye;
                not.SnapshotTarihi = now;
            }
        }

        private async Task<HashSet<string>> HedefKodlariAsync(int firmaId, int hedefYil, CancellationToken ct)
        {
            var kodlar = await _db.MizanNotlari
                .AsNoTracking()
                .Where(n => n.FirmaId == firmaId && n.DonemYili == hedefYil)
                .Select(n => n.HesapKodu)
                .ToListAsync(ct);

            return new HashSet<string>(kodlar, StringComparer.OrdinalIgnoreCase);
        }

        private async Task EnsureFirmaExistsAsync(int firmaId, CancellationToken ct)
        {
            var exists = await _db.Firmalar.AnyAsync(f => f.Id == firmaId, ct);
            if (!exists)
                throw new KeyNotFoundException($"Firma bulunamadı: Id={firmaId}");
        }

        private static string Kirp(string metin) =>
            metin.Length <= MetinMaxUzunluk ? metin : metin[..MetinMaxUzunluk];

        private static MizanNotuDto MapToDto(MizanNotu n) => new()
        {
            Id = n.Id,
            HesapKodu = n.HesapKodu,
            AnaHesapKodu = MizanNotu.AnaHesapKodu(n.HesapKodu),
            Metin = n.Metin,
            NotTuru = n.NotTuru,
            DonemYili = n.DonemYili,
            UyariBastir = n.UyariBastir,
            SnapshotBorc = n.SnapshotBorc,
            SnapshotAlacak = n.SnapshotAlacak,
            SnapshotBakiye = n.SnapshotBakiye,
            SnapshotTarihi = n.SnapshotTarihi,
            CreatedAt = n.CreatedAt,
            UpdatedAt = n.UpdatedAt
        };

        /// <summary>Devredilen notun sonuna eklenen etiket: "(2025'ten devir)".</summary>
        private static string DevirEtiketi(int kaynakYil) =>
            $"({kaynakYil}{AblatifEki(kaynakYil)} devir)";

        /// <summary>
        /// Yıl rakamının okunuşuna göre ayrılma hâli eki. Ek, sayının SON okunan
        /// kelimesine uyar: 2025 "…yirmi beş" → 'ten, 2026 "…yirmi altı" → 'dan.
        /// </summary>
        private static string AblatifEki(int yil)
        {
            var sonIki = yil % 100;
            var sonBir = yil % 10;

            // 2000, 2100… → "iki bin" / "iki bin yüz" → ince, yumuşak: 'den
            if (sonIki == 0) return "'den";

            if (sonBir != 0)
                return sonBir switch
                {
                    1 => "'den", // bir
                    2 => "'den", // iki
                    3 => "'ten", // üç
                    4 => "'ten", // dört
                    5 => "'ten", // beş
                    6 => "'dan", // altı
                    7 => "'den", // yedi
                    8 => "'den", // sekiz
                    _ => "'dan"  // dokuz
                };

            return (sonIki / 10) switch
            {
                1 => "'dan", // on
                2 => "'den", // yirmi
                3 => "'dan", // otuz
                4 => "'tan", // kırk
                5 => "'den", // elli
                6 => "'tan", // altmış
                7 => "'ten", // yetmiş
                8 => "'den", // seksen
                _ => "'dan"  // doksan
            };
        }
    }
}

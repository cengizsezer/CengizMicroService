using WebApp.Application.RuleEngine;
using WebApp.Application.Services.FirmaKontrol;
using WebApp.Application.Services.Interfaces;
using WebApp.Application.Services.Yonetim;
using WebApp.Domain.Models.FirmaKontrol;
using WebApp.Shared.Dto.FirmaKontrol;
using WebApp.Shared.Dto.Yonetim;

namespace WebApp.Application.Services
{
    public class MockFirmaKontrolService : IFirmaKontrolService
    {
        public const string CatDonenVarliklar = "Dönen Varlıklar";
        public const string CatDuranVarlik = "Duran Varlık";
        public const string CatKisaVY = "Kısa V.Y";
        public const string CatBilancoIslemi = "Bilanço İşlemi";
        public const string CatGelirTablosu = "Gelir Tablosu";
        public const string CatBeyannameSonuc = "Beyanname Sonuç";
        public const string CatTicaretSicil = "Ticaret Sicil";

        // Firmaya özel (kullanıcı eklemiş) maddelerin kategorisi. CategoryOrder'a
        // dahil DEĞİL — UI'da ayrı "Özel" bölümünde gösterilir.
        public const string CatOzel = "Özel";

        public static readonly IReadOnlyList<string> CategoryOrder = new[]
        {
            CatDonenVarliklar,
            CatDuranVarlik,
            CatKisaVY,
            CatBilancoIslemi,
            CatGelirTablosu,
            CatBeyannameSonuc,
            CatTicaretSicil
        };

        private readonly IHesapPlaniLoader _hesapPlaniLoader;
        private readonly MizanRuleEngine _ruleEngine;
        private readonly IFirmaApiClient _firmaApiClient;
        private readonly IFirmaKontrolApiClient _kontrolApiClient;
        private readonly List<Firma> _firms = new();
        private readonly Dictionary<int, List<ControlItem>> _itemsByFirm = new();
        private readonly Dictionary<int, Dictionary<string, decimal?>> _rawCariByFirm = new();
        private readonly Dictionary<int, Dictionary<string, decimal?>> _rawOncekiByFirm = new();
        private readonly Dictionary<int, Dictionary<string, string>> _rawAdByFirm = new();

        private readonly SemaphoreSlim _initLock = new(1, 1);
        private readonly SemaphoreSlim _firmsLock = new(1, 1);

        // Mizan DB'den bir kez (scope ömrü boyunca) hidre edilir; F5'te yeni scope
        // olduğundan DB'den tekrar taze çekilir. Hafif in-memory cache — over-engineer yok.
        private readonly SemaphoreSlim _mizanHydrateLock = new(1, 1);
        private readonly HashSet<int> _mizanHydratedFirms = new();

        // Vergi paneli girdileri için aynı hafif hidrasyon (scope ömrü, F5'te taze).
        private readonly SemaphoreSlim _vergiHydrateLock = new(1, 1);
        private readonly HashSet<int> _vergiHydratedFirms = new();

        // Mizan notları da aynı desende: firma başına TEK çağrı, scope ömrü boyunca
        // bellekte. Mizan hidrasyonundan ayrı tutulur ki not API'si hata verirse
        // mizan yüklemesi etkilenmesin (ve tersi).
        private readonly Dictionary<int, List<MizanNotuDto>> _notlarByFirm = new();
        private readonly SemaphoreSlim _notHydrateLock = new(1, 1);
        private readonly HashSet<int> _notHydratedFirms = new();

        private HesapPlani? _hesapPlaniTemplate;
        private bool _mizanInitialized;
        private bool _firmsLoaded;

        // Seçili hesap dönemi yılı — hem yazma hem okuma bu yıl üzerinden gider.
        // Firma Kontrol ekranındaki dönem seçicisi burayı değiştirir; seçim scope
        // ömrüyle sınırlıdır (F5'te içinde bulunulan yıla döner).
        private int _donemYili = DateTime.Now.Year;

        private int CurrentYil => _donemYili;

        public MockFirmaKontrolService(
            IHesapPlaniLoader hesapPlaniLoader,
            MizanRuleEngine ruleEngine,
            IFirmaApiClient firmaApiClient,
            IFirmaKontrolApiClient kontrolApiClient)
        {
            _hesapPlaniLoader = hesapPlaniLoader;
            _ruleEngine = ruleEngine;
            _firmaApiClient = firmaApiClient;
            _kontrolApiClient = kontrolApiClient;
        }

        // Firma listesini gerçek "Firmalarım" kaynağından (CatalogService Firma tablosu)
        // tek seferlik yükler. Servis Scoped olduğundan, auth+tenant header pipeline'ı
        // bağlı scoped IFirmaApiClient'ı doğrudan enjekte edip kullanıyoruz (Firmalarım ile
        // birebir aynı client). Kontrol şablonu ve Mizan, her gerçek firma için lazy üretilir
        // — kalıcılık yok, geçici iskelet.
        private async Task EnsureFirmsLoadedAsync()
        {
            if (_firmsLoaded) return;

            await _firmsLock.WaitAsync();
            try
            {
                if (_firmsLoaded) return;

                List<FirmaDto> gercekFirmalar;
                try
                {
                    gercekFirmalar = await _firmaApiClient.GetAllAsync();
                }
                catch (Exception ex)
                {
                    // API çağrısı başarısız: bayrağı YAKMA, sonraki erişimde tekrar denensin.
                    // Hatayı tarayıcı console'una yüzeye çıkar (sessiz yutma yok).
                    Console.WriteLine($"[FirmaLoad HATA] {ex}");
                    return;
                }

                // Boş liste (henüz firma yok ya da kaynak hazır değil): bayrağı YAKMA,
                // sonraki erişimde tekrar denensin. Aksi halde singleton kalıcı kilitlenir.
                if (gercekFirmalar.Count == 0)
                    return;

                foreach (var dto in gercekFirmalar)
                {
                    if (_firms.Any(f => f.Id == dto.Id)) continue;

                    _firms.Add(new Firma
                    {
                        Id = dto.Id,
                        Ad = string.IsNullOrWhiteSpace(dto.Unvan) ? dto.KisaAd : dto.Unvan,
                        VergiNo = dto.VergiKimlikNo,
                        Sektor = string.Empty,
                        Donem = $"{DateTime.Now.Year} Hesap Dönemi",
                        LogoText = BuildLogoText(dto),
                        Mizan = _hesapPlaniTemplate?.Clone() ?? new HesapPlani()
                    });

                    if (!_itemsByFirm.ContainsKey(dto.Id))
                        _itemsByFirm[dto.Id] = BuildTemplate();
                }

                _firmsLoaded = true;
            }
            finally
            {
                _firmsLock.Release();
            }
        }

        private static string BuildLogoText(FirmaDto dto)
        {
            var kaynak = !string.IsNullOrWhiteSpace(dto.KisaAd) ? dto.KisaAd : dto.Unvan;
            if (string.IsNullOrWhiteSpace(kaynak)) return "?";

            var parcalar = kaynak.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parcalar.Length >= 2)
                return $"{char.ToUpperInvariant(parcalar[0][0])}{char.ToUpperInvariant(parcalar[1][0])}";

            return kaynak[..Math.Min(2, kaynak.Length)].ToUpperInvariant();
        }

        public async Task<IReadOnlyList<Firma>> GetFirmsAsync()
        {
            await EnsureFirmsLoadedAsync();
            return _firms;
        }

        public async Task<Firma?> GetFirmAsync(int firmaId)
        {
            await EnsureFirmsLoadedAsync();
            return _firms.FirstOrDefault(f => f.Id == firmaId);
        }

        public async Task<IReadOnlyList<ControlItem>> GetControlItemsAsync(int firmaId)
        {
            // 46 şablon maddesini koddan üret (metin + stabil MaddeKey burada).
            var template = BuildTemplate();

            // DB'de saklı durumları çek (şablon durumları + özel maddeler).
            List<FirmaKontrolMaddeDto> durumlar;
            try
            {
                durumlar = await _kontrolApiClient.GetMaddelerAsync(firmaId);
            }
            catch (Exception ex)
            {
                // API başarısız: en azından boş şablonu göster, hatayı yüzeye çıkar.
                Console.WriteLine($"[KontrolLoad HATA] {ex}");
                durumlar = new List<FirmaKontrolMaddeDto>();
            }

            // Şablon durumlarını MaddeKey ile eşle ve template'e uygula.
            var durumByKey = durumlar
                .Where(d => !d.IsCustom && !string.IsNullOrWhiteSpace(d.MaddeKey))
                .GroupBy(d => d.MaddeKey!, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            foreach (var item in template)
            {
                if (item.MaddeKey is not null && durumByKey.TryGetValue(item.MaddeKey, out var d))
                {
                    item.IsChecked = d.IsChecked;
                    item.Status = (ControlStatus)d.Status;
                    item.Note = d.Not;
                }
            }

            // Özel maddeleri (firmaya özgü) ekle.
            var custom = durumlar
                .Where(d => d.IsCustom)
                .OrderBy(d => d.SiraNo)
                .ThenBy(d => d.Id)
                .Select(d => new ControlItem
                {
                    Id = (int)d.Id,
                    MaddeKey = null,
                    IsCustom = true,
                    Category = string.IsNullOrWhiteSpace(d.Category) ? CatOzel : d.Category,
                    Question = d.SoruMetni ?? string.Empty,
                    IsChecked = d.IsChecked,
                    Status = (ControlStatus)d.Status,
                    Note = d.Not
                });

            var result = template.Concat(custom).ToList();
            _itemsByFirm[firmaId] = result;
            return result;
        }

        public async Task UpdateControlItemAsync(int firmaId, ControlItem item)
        {
            // Şablon maddesi: MaddeKey ile upsert. Özel madde: Id ile.
            var dto = new FirmaKontrolMaddeUpsertDto
            {
                MaddeKey = item.MaddeKey,
                Id = item.IsCustom ? item.Id : null,
                IsCustom = item.IsCustom,
                Category = item.Category,
                IsChecked = item.IsChecked,
                Status = (int)item.Status,
                Not = item.Note
            };

            await _kontrolApiClient.UpsertMaddeAsync(firmaId, dto);
        }

        public async Task<ControlItem> AddOzelKontrolMaddesiAsync(int firmaId, string category, string soruMetni)
        {
            var dto = new OzelMaddeCreateDto
            {
                Category = string.IsNullOrWhiteSpace(category) ? CatOzel : category,
                SoruMetni = soruMetni
            };

            var created = await _kontrolApiClient.AddOzelAsync(firmaId, dto);

            return new ControlItem
            {
                Id = (int)created.Id,
                MaddeKey = null,
                IsCustom = true,
                Category = string.IsNullOrWhiteSpace(created.Category) ? CatOzel : created.Category,
                Question = created.SoruMetni ?? string.Empty,
                IsChecked = created.IsChecked,
                Status = (ControlStatus)created.Status,
                Note = created.Not
            };
        }

        public async Task UpdateOzelKontrolMaddesiAsync(int firmaId, int id, string yeniMetin)
        {
            await _kontrolApiClient.UpdateOzelAsync(firmaId, id, new OzelMaddeUpdateDto
            {
                SoruMetni = yeniMetin
            });
        }

        public async Task DeleteOzelKontrolMaddesiAsync(int firmaId, int id)
        {
            await _kontrolApiClient.DeleteOzelAsync(firmaId, id);
        }

        public async Task<HesapPlani> GetMizanAsync(int firmaId)
        {
            await EnsureFirmMizanHydratedAsync(firmaId);

            var firma = _firms.FirstOrDefault(f => f.Id == firmaId);
            return firma?.Mizan ?? new HesapPlani();
        }

        public async Task<MizanUpdateResult> UpdateMizanFromExcelAsync(int firmaId, MizanParseResult parseResult, Donem donem)
        {
            // Önce mevcut (kalıcı) mizanı hidre et: diğer dönemin DB verisi belleğe gelsin,
            // yeni yükleme onun üzerine yazsın (Cari yüklerken Onceki kaybolmasın).
            await EnsureFirmMizanHydratedAsync(firmaId);

            var result = new MizanUpdateResult();

            // Parser'ın elidiği satırları (hiyerarşik / geçersiz format / bakiye yok)
            // detay listesine al — sebep bazlı gruplandırma için UI bunları kullanır.
            result.AtlananSatirlar.AddRange(parseResult.AtlananSatirlar);

            var rows = parseResult.Rows;
            var firma = _firms.FirstOrDefault(f => f.Id == firmaId);
            if (firma is null) return result;

            // Tek bir lookup map: hesap kodu -> tüm sections'taki MizanSatir referansları
            var lookup = new Dictionary<string, List<MizanSatir>>(StringComparer.OrdinalIgnoreCase);

            void Index(IEnumerable<MizanSatir> section)
            {
                foreach (var s in section)
                {
                    if (s.Tip != SatirTipi.Account) continue;
                    if (string.IsNullOrWhiteSpace(s.Kod)) continue;

                    if (!lookup.TryGetValue(s.Kod, out var bucket))
                    {
                        bucket = new List<MizanSatir>();
                        lookup[s.Kod] = bucket;
                    }
                    bucket.Add(s);
                }
            }

            Index(firma.Mizan.Aktif);
            Index(firma.Mizan.Pasif);
            Index(firma.Mizan.GelirTablosu);

            // Plan'da bulunma kontrolü için TÜM tipleri (Account/Total/SubGroup)
            // kapsayan kod seti. 690 (Total), 691 (SubGroup), 692 (Total) gibi
            // satırlar Account olmasa da plan'da kabul edilir; mizandan gelirse
            // raw map'e yazılır (gelir tablosu/vergi paneli buradan okur), satır
            // güncellemesi ise Account olmadığı için yapılmaz — bu da matched sayılır.
            var planKodSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            void IndexAllKodlar(IEnumerable<MizanSatir> section)
            {
                foreach (var s in section)
                {
                    if (!string.IsNullOrWhiteSpace(s.Kod))
                        planKodSet.Add(s.Kod);
                }
            }
            IndexAllKodlar(firma.Mizan.Aktif);
            IndexAllKodlar(firma.Mizan.Pasif);
            IndexAllKodlar(firma.Mizan.GelirTablosu);

            // Raw değer haritasını sıfırla — bu yükleme firmanın seçili dönem raw'ını değiştirir
            var raw = new Dictionary<string, decimal?>(StringComparer.OrdinalIgnoreCase);

            // Mizandan gelen ad'lar (kod -> ad). Yeni yüklemede mevcut ad map'i ile birleşiyoruz
            // ki Cari yüklemesi Onceki'nin ad'larını silmesin.
            if (!_rawAdByFirm.TryGetValue(firmaId, out var adMap))
            {
                adMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                _rawAdByFirm[firmaId] = adMap;
            }

            foreach (var row in rows)
            {
                if (string.IsNullOrWhiteSpace(row.Kod)) continue;

                // Excel'den gelen "CariDonem" alanı, dönemden bağımsız olarak yüklenen değerdir.
                // Donem parametresi bu değerin Onceki mi Cari döneme yazılacağını belirler.
                var value = row.CariDonem ?? row.OncekiDonem;
                if (!value.HasValue) continue;

                raw[row.Kod] = value;

                // Mizandan gelen ad varsa, kod bazlı ad map'ine yaz (sonradan gelen "—" değerler ezmesin)
                if (!string.IsNullOrWhiteSpace(row.Ad))
                    adMap[row.Kod] = row.Ad!.Trim();

                if (lookup.TryGetValue(row.Kod, out var targets))
                {
                    foreach (var t in targets)
                    {
                        if (donem == Donem.Cari) t.CariDonem = value;
                        else t.OncekiDonem = value;

                        // Fallback: HesapPlani'nde ad boşsa mizandaki ad ile doldur
                        if (string.IsNullOrWhiteSpace(t.Ad) && !string.IsNullOrWhiteSpace(row.Ad))
                            t.Ad = row.Ad!.Trim();
                    }
                    result.Matched++;
                }
                else if (planKodSet.Contains(row.Kod))
                {
                    // Plan'da var ama Account değil (Total/SubGroup — örn 690/691/692).
                    // Account satır güncellemesi yok; raw map (yukarıda) zaten dolduruldu,
                    // gelir tablosu calculator ve vergi paneli buradan okuyor.
                    result.Matched++;
                }
                else
                {
                    result.Unmatched++;
                    if (result.UnmatchedKodlar.Count < 50)
                        result.UnmatchedKodlar.Add(row.Kod);

                    result.AtlananSatirlar.Add(new AtlananSatir
                    {
                        Kod = row.Kod,
                        Ad = row.Ad,
                        Bakiye = value,
                        Sebep = AtlamaSebebi.PlandaBulunamadi,
                        SebepMetni = "Hesap Planında Bulunamadı"
                    });
                }
            }

            if (donem == Donem.Cari)
                _rawCariByFirm[firmaId] = raw;
            else
                _rawOncekiByFirm[firmaId] = raw;

            // Ham satırları DB'ye kalıcı yaz — idempotent (bu firma+dönem+yıl için sil+yaz).
            // Excel parse'ı client-side kaldı; sadece parse edilmiş ham değerler gönderilir.
            try
            {
                var req = new MizanKaydetRequest
                {
                    Donem = (int)donem,
                    Yil = CurrentYil,
                    Satirlar = raw.Select(kv => new MizanHamSatirDto
                    {
                        Kod = kv.Key,
                        Ad = adMap.TryGetValue(kv.Key, out var ad) ? ad : null,
                        Bakiye = kv.Value
                    }).ToList()
                };
                await _kontrolApiClient.SaveMizanAsync(firmaId, req);

                // Bu scope'ta bellek artık DB ile aynı — gereksiz yeniden fetch etme.
                _mizanHydratedFirms.Add(firmaId);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MizanSave HATA] {ex}");
            }

            return result;
        }

        public async Task<IReadOnlyDictionary<string, decimal?>> GetRawMizanValuesAsync(int firmaId, Donem donem)
        {
            await EnsureFirmMizanHydratedAsync(firmaId);

            var source = donem == Donem.Cari ? _rawCariByFirm : _rawOncekiByFirm;
            if (source.TryGetValue(firmaId, out var map))
                return map;

            return new Dictionary<string, decimal?>(StringComparer.OrdinalIgnoreCase);
        }

        public async Task<IReadOnlyDictionary<string, string>> GetRawMizanAdlarAsync(int firmaId)
        {
            await EnsureFirmMizanHydratedAsync(firmaId);

            if (_rawAdByFirm.TryGetValue(firmaId, out var map))
                return map;

            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        public async Task<VergiHesaplama> GetVergiBilgisiAsync(int firmaId)
        {
            await EnsureFirmVergiHydratedAsync(firmaId);

            var firma = _firms.FirstOrDefault(f => f.Id == firmaId);
            return firma?.VergiBilgisiCariDonem ?? new VergiHesaplama();
        }

        public async Task SaveVergiBilgisiAsync(int firmaId, VergiHesaplama vergi)
        {
            // Bellekteki referansı da güncel tut (UI bu nesneyi zaten mutasyona uğratıyor).
            var firma = _firms.FirstOrDefault(f => f.Id == firmaId);
            if (firma is not null) firma.VergiBilgisiCariDonem = vergi;

            var dto = MapVergiToDto(vergi, (int)Donem.Cari, CurrentYil);
            await _kontrolApiClient.SaveVergiAsync(firmaId, dto);

            // Bellek = DB; sonraki erişimde gereksiz yeniden fetch etme.
            _vergiHydratedFirms.Add(firmaId);
        }

        // Vergi girdilerini DB'den bir kez (scope ömrü) belleğe yükler. F5'te yeni scope
        // olduğundan tekrar taze çekilir. API hata verirse bayrak yakılmaz.
        private async Task EnsureFirmVergiHydratedAsync(int firmaId)
        {
            await EnsureFirmsLoadedAsync();
            if (_vergiHydratedFirms.Contains(firmaId)) return;

            await _vergiHydrateLock.WaitAsync();
            try
            {
                if (_vergiHydratedFirms.Contains(firmaId)) return;

                var firma = _firms.FirstOrDefault(f => f.Id == firmaId);
                if (firma is null) return;

                FirmaKontrolVergiDto? dto;
                try
                {
                    dto = await _kontrolApiClient.GetVergiAsync(firmaId, (int)Donem.Cari, CurrentYil);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[VergiLoad HATA] {ex}");
                    return; // bayrağı yakma → sonraki erişimde tekrar dene
                }

                // Kayıt varsa belleğe uygula; yoksa mevcut (boş) VergiHesaplama kalır.
                if (dto is not null)
                    firma.VergiBilgisiCariDonem = MapDtoToVergi(dto);

                _vergiHydratedFirms.Add(firmaId);
            }
            finally
            {
                _vergiHydrateLock.Release();
            }
        }

        private static FirmaKontrolVergiDto MapVergiToDto(VergiHesaplama v, int donem, int yil) => new()
        {
            Donem = donem,
            Yil = yil,
            Kkeg = v.Kkeg,
            KkegIstisna = v.KkegIstisna,
            GecmisYil_2024 = v.GecmisYil_2024,
            GecmisYil_2023 = v.GecmisYil_2023,
            GecmisYil_2022 = v.GecmisYil_2022,
            GecmisYil_2021 = v.GecmisYil_2021,
            TemettuGeliri = v.TemettuGeliri,
            BagisYardim = v.BagisYardim,
            Kv5Indirim = v.Kv5Indirim,
            GeciciVergi = v.GeciciVergi,
            BankaStopaji = v.BankaStopaji,
            DigerTevkifat = v.DigerTevkifat
        };

        private static VergiHesaplama MapDtoToVergi(FirmaKontrolVergiDto d) => new()
        {
            Kkeg = d.Kkeg,
            KkegIstisna = d.KkegIstisna,
            GecmisYil_2024 = d.GecmisYil_2024,
            GecmisYil_2023 = d.GecmisYil_2023,
            GecmisYil_2022 = d.GecmisYil_2022,
            GecmisYil_2021 = d.GecmisYil_2021,
            TemettuGeliri = d.TemettuGeliri,
            BagisYardim = d.BagisYardim,
            Kv5Indirim = d.Kv5Indirim,
            GeciciVergi = d.GeciciVergi,
            BankaStopaji = d.BankaStopaji,
            DigerTevkifat = d.DigerTevkifat
        };

        public async Task<IReadOnlyList<UyariSonucu>> GetUyarilarAsync(int firmaId)
        {
            await EnsureFirmMizanHydratedAsync(firmaId);

            var firma = _firms.FirstOrDefault(f => f.Id == firmaId);
            if (firma is null) return Array.Empty<UyariSonucu>();

            _rawCariByFirm.TryGetValue(firmaId, out var rawCari);
            _rawOncekiByFirm.TryGetValue(firmaId, out var rawOnceki);

            var context = new MizanRuleContext
            {
                Firma = firma,
                Mizan = firma.Mizan,
                RawCariValues = rawCari ?? new Dictionary<string, decimal?>(StringComparer.OrdinalIgnoreCase),
                RawOncekiValues = rawOnceki ?? new Dictionary<string, decimal?>(StringComparer.OrdinalIgnoreCase),
                Esikler = MizanEsikler.Default()
            };

            return _ruleEngine.Calistir(context);
        }

        // ── Mizan hesap notları ─────────────────────────────────────────────

        public int AktifDonemYili => _donemYili;

        public void SetDonemYili(int yil)
        {
            if (yil == _donemYili) return;

            _donemYili = yil;

            // Döneme bağlı her şey düşer. Hidrasyon API hatası alsa bile eski yılın
            // verisi ekranda KALMAMALI — bu yüzden bayrakların yanında bellekteki
            // nesneler de sıfırlanıyor.
            _mizanHydratedFirms.Clear();
            _rawCariByFirm.Clear();
            _rawOncekiByFirm.Clear();
            _rawAdByFirm.Clear();

            _notHydratedFirms.Clear();
            _notlarByFirm.Clear();

            _vergiHydratedFirms.Clear();

            foreach (var firma in _firms)
            {
                if (_hesapPlaniTemplate is not null)
                    firma.Mizan = _hesapPlaniTemplate.Clone();

                // Vergi hidrasyonu kayıt yoksa üzerine yazmıyor; burada sıfırlanmazsa
                // önceki dönemin girdileri yeni dönemde görünmeye devam ederdi.
                firma.VergiBilgisiCariDonem = new VergiHesaplama();
            }

            // Kontrol maddeleri (FirmaKontrolMadde) döneme bağlı DEĞİL — dokunulmuyor.
        }

        public async Task<IReadOnlyList<MizanNotuDto>> GetMizanNotlariAsync(int firmaId)
        {
            await EnsureNotlarHydratedAsync(firmaId);

            // Kopya döndürülür: çağıran listeyi sıralayıp filtreleyebilsin, cache bozulmasın.
            return _notlarByFirm.TryGetValue(firmaId, out var liste)
                ? liste.ToList()
                : Array.Empty<MizanNotuDto>();
        }

        public async Task<MizanNotuDto> SaveMizanNotuAsync(int firmaId, MizanNotuUpsertDto dto)
        {
            await EnsureNotlarHydratedAsync(firmaId);

            var kaydedilen = await _kontrolApiClient.UpsertMizanNotuAsync(firmaId, dto);

            // Bellek DB ile aynı kalsın: aynı Id varsa değiştir, yoksa ekle.
            var liste = NotListesi(firmaId);
            var idx = liste.FindIndex(n => n.Id == kaydedilen.Id);
            if (idx >= 0) liste[idx] = kaydedilen;
            else liste.Add(kaydedilen);

            return kaydedilen;
        }

        public async Task<MizanNotuDto> UpdateMizanNotuAsync(int firmaId, long id, MizanNotuGuncelleDto dto)
        {
            await EnsureNotlarHydratedAsync(firmaId);

            var guncellenen = await _kontrolApiClient.GuncelleMizanNotuAsync(firmaId, id, dto);

            // Tip değişmiş olabilir (DonemYili) — kaydı sunucudan dönen haliyle değiştir.
            var liste = NotListesi(firmaId);
            var idx = liste.FindIndex(n => n.Id == guncellenen.Id);
            if (idx >= 0) liste[idx] = guncellenen;
            else liste.Add(guncellenen);

            return guncellenen;
        }

        public async Task<MizanNotuDto> SnapshotYenileAsync(int firmaId, long id)
        {
            await EnsureNotlarHydratedAsync(firmaId);

            var yenilenen = await _kontrolApiClient.SnapshotYenileAsync(firmaId, id);

            var liste = NotListesi(firmaId);
            var idx = liste.FindIndex(n => n.Id == yenilenen.Id);
            if (idx >= 0) liste[idx] = yenilenen;
            else liste.Add(yenilenen);

            return yenilenen;
        }

        public async Task DeleteMizanNotuAsync(int firmaId, long id)
        {
            await EnsureNotlarHydratedAsync(firmaId);

            await _kontrolApiClient.DeleteMizanNotuAsync(firmaId, id);
            NotListesi(firmaId).RemoveAll(n => n.Id == id);
        }

        public async Task<IReadOnlyList<MizanNotuDto>> GetNotDevirAdaylariAsync(int firmaId, int kaynakYil, int hedefYil) =>
            await _kontrolApiClient.GetDevirAdaylariAsync(firmaId, kaynakYil, hedefYil);

        public async Task<IReadOnlyList<MizanNotuDto>> DevretMizanNotlariAsync(int firmaId, MizanNotuDevirRequest req)
        {
            await EnsureNotlarHydratedAsync(firmaId);

            var yeniler = await _kontrolApiClient.DevretMizanNotlariAsync(firmaId, req);

            // Hedef aktif dönemse taşınan notlar ekranda hemen görünsün.
            if (req.HedefYil == CurrentYil && yeniler.Count > 0)
                NotListesi(firmaId).AddRange(yeniler);

            return yeniler;
        }

        // Firmanın notlarını DB'den bir kez (scope ömrü) belleğe yükler. Kalıcı notlar
        // + aktif dönemin notları tek çağrıda gelir. API hata verirse bayrak YAKILMAZ.
        private async Task EnsureNotlarHydratedAsync(int firmaId)
        {
            if (_notHydratedFirms.Contains(firmaId)) return;

            await _notHydrateLock.WaitAsync();
            try
            {
                if (_notHydratedFirms.Contains(firmaId)) return;

                List<MizanNotuDto> notlar;
                try
                {
                    notlar = await _kontrolApiClient.GetMizanNotlariAsync(firmaId, CurrentYil);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[MizanNotLoad HATA] {ex}");
                    return;
                }

                _notlarByFirm[firmaId] = notlar;
                _notHydratedFirms.Add(firmaId);
            }
            finally
            {
                _notHydrateLock.Release();
            }
        }

        private List<MizanNotuDto> NotListesi(int firmaId)
        {
            if (!_notlarByFirm.TryGetValue(firmaId, out var liste))
            {
                liste = new List<MizanNotuDto>();
                _notlarByFirm[firmaId] = liste;
            }

            return liste;
        }

        public async Task ResetMizanAsync(int firmaId)
        {
            await EnsureMizanInitializedAsync();

            var firma = _firms.FirstOrDefault(f => f.Id == firmaId);
            if (firma is null || _hesapPlaniTemplate is null) return;
            firma.Mizan = _hesapPlaniTemplate.Clone();
            _rawCariByFirm.Remove(firmaId);
            _rawOncekiByFirm.Remove(firmaId);
            _rawAdByFirm.Remove(firmaId);

            // DB'den de kalıcı olarak temizle; bellek artık DB ile aynı (boş).
            try
            {
                await _kontrolApiClient.DeleteMizanAsync(firmaId, CurrentYil);
                _mizanHydratedFirms.Add(firmaId);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MizanReset HATA] {ex}");
            }
        }

        private async Task EnsureMizanInitializedAsync()
        {
            if (_mizanInitialized) return;

            await _initLock.WaitAsync();
            try
            {
                if (_mizanInitialized) return;

                _hesapPlaniTemplate = await _hesapPlaniLoader.LoadAsync();

                // Gerçek firmalar henüz yüklenmediyse yükle (Mizan'ı bunlara klonlayacağız).
                await EnsureFirmsLoadedAsync();

                foreach (var f in _firms)
                    f.Mizan = _hesapPlaniTemplate.Clone();

                _mizanInitialized = true;
            }
            finally
            {
                _initLock.Release();
            }
        }

        // Firmanın kalıcı mizanını DB'den bir kez (scope ömrü) belleğe yükler. F5'te
        // yeni scope olduğundan tekrar taze çekilir. API hata verirse bayrak yakılmaz.
        private async Task EnsureFirmMizanHydratedAsync(int firmaId)
        {
            await EnsureMizanInitializedAsync();
            if (_mizanHydratedFirms.Contains(firmaId)) return;

            await _mizanHydrateLock.WaitAsync();
            try
            {
                if (_mizanHydratedFirms.Contains(firmaId)) return;

                var firma = _firms.FirstOrDefault(f => f.Id == firmaId);
                if (firma is null) return;

                List<FirmaKontrolMizanSatirDto> rows;
                try
                {
                    rows = await _kontrolApiClient.GetMizanAsync(firmaId, CurrentYil);
                }
                catch (Exception ex)
                {
                    // API başarısız: bayrağı YAKMA, sonraki erişimde tekrar denensin.
                    Console.WriteLine($"[MizanLoad HATA] {ex}");
                    return;
                }

                var rawCari = new Dictionary<string, decimal?>(StringComparer.OrdinalIgnoreCase);
                var rawOnceki = new Dictionary<string, decimal?>(StringComparer.OrdinalIgnoreCase);
                var adMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                foreach (var r in rows)
                {
                    if (string.IsNullOrWhiteSpace(r.Kod)) continue;
                    if ((Donem)r.Donem == Donem.Cari) rawCari[r.Kod] = r.Bakiye;
                    else rawOnceki[r.Kod] = r.Bakiye;
                    if (!string.IsNullOrWhiteSpace(r.Ad)) adMap[r.Kod] = r.Ad;
                }

                // Şablon plandan taze klon + her iki dönemi ham değerlerle doldur.
                if (_hesapPlaniTemplate is not null)
                    firma.Mizan = _hesapPlaniTemplate.Clone();

                FillMizanValues(firma.Mizan, rawOnceki, Donem.Onceki, adMap);
                FillMizanValues(firma.Mizan, rawCari, Donem.Cari, adMap);

                _rawOncekiByFirm[firmaId] = rawOnceki;
                _rawCariByFirm[firmaId] = rawCari;
                _rawAdByFirm[firmaId] = adMap;

                _mizanHydratedFirms.Add(firmaId);
            }
            finally
            {
                _mizanHydrateLock.Release();
            }
        }

        // DB'den gelen ham değerleri (kod -> bakiye) HesapPlani'nin ilgili dönem
        // hücrelerine uygular. UpdateMizanFromExcelAsync'teki lookup ile aynı kural:
        // yalnızca Account satırları; plandaki ad boşsa mizan ad map'inden doldur.
        // Non-Account kodlar (690/691/692 vb.) raw map'lerde kalır; hücre yazılmaz —
        // gelir tablosu calculator ve vergi paneli o kodları raw'dan okur.
        private static void FillMizanValues(
            HesapPlani mizan,
            IReadOnlyDictionary<string, decimal?> raw,
            Donem donem,
            IReadOnlyDictionary<string, string> adMap)
        {
            if (raw.Count == 0) return;

            var lookup = new Dictionary<string, List<MizanSatir>>(StringComparer.OrdinalIgnoreCase);
            void Index(IEnumerable<MizanSatir> section)
            {
                foreach (var s in section)
                {
                    if (s.Tip != SatirTipi.Account) continue;
                    if (string.IsNullOrWhiteSpace(s.Kod)) continue;
                    if (!lookup.TryGetValue(s.Kod, out var bucket))
                    {
                        bucket = new List<MizanSatir>();
                        lookup[s.Kod] = bucket;
                    }
                    bucket.Add(s);
                }
            }
            Index(mizan.Aktif);
            Index(mizan.Pasif);
            Index(mizan.GelirTablosu);

            foreach (var kv in raw)
            {
                if (!lookup.TryGetValue(kv.Key, out var targets)) continue;
                foreach (var t in targets)
                {
                    if (donem == Donem.Cari) t.CariDonem = kv.Value;
                    else t.OncekiDonem = kv.Value;

                    if (string.IsNullOrWhiteSpace(t.Ad)
                        && adMap.TryGetValue(kv.Key, out var ad)
                        && !string.IsNullOrWhiteSpace(ad))
                        t.Ad = ad;
                }
            }
        }

        private static List<ControlItem> BuildTemplate()
        {
            var items = new List<ControlItem>();
            int id = 1;

            // key = STABİL anahtar (örn "DV-01"). Durum/not DB'de bu anahtara bağlanır;
            // sıraya/Id'ye DEĞİL. Bir maddenin anahtarı bir kez verildikten sonra ASLA
            // değişmemeli — aksi halde kayıtlı durumlar yanlış maddeye bağlanır.
            void Add(string cat, string key, string q) =>
                items.Add(new ControlItem { Id = id++, MaddeKey = key, Category = cat, Question = q });

            // 1. Dönen Varlıklar (11)
            Add(CatDonenVarliklar, "DV-01", "Dövizli kasa bankası çek cari hesap vb var mı, değerlemesi yapılmış mı? Kasa efektif döviz alış, diğer hesaplar döviz alış kuru ile değerle.");
            Add(CatDonenVarliklar, "DV-02", "Kasa sorunu var mı? (Kasanın olağandan fazla bakiye vermesi) Dönem içinde kasanın eksiye düştüğü günler var mı? Örnek kasa sayfasına bak.");
            Add(CatDonenVarliklar, "DV-03", "30.000 TL üzeri nakit ödeme var mı, varsa ilişkisi nedir? Cari hesap ise detayı kontrol edilmeli.");
            Add(CatDonenVarliklar, "DV-04", "101-103 Hesapların devri vadeye göre uygun mu? 31.12 hafta sonuna denk geliyor ise, çeklerin devri Ocak ve sonraki aylara ait olacak şekilde olmalıdır.");
            Add(CatDonenVarliklar, "DV-05", "Banka bakiyelerin 31.12 tarihindeki bakiyesini vermesi gerekir. POS bakiyesi ise banka ile anlaşma bakiyesine uygun olmalı. Örneğin ertesi gün tahsil anlaşması var ise hafta tatil gününe göre 30.12 ve 31.12 bakiyesi devir olmalı. 30 günlük tahsilat anlaşması var ise 1 Aralık - 31 Aralık tarihleri arası tahsilat bakiyesinin kalması gerekir.");
            Add(CatDonenVarliklar, "DV-06", "Verilen çekler hesabı hazır değerler grubunda eksi karakterli çalışan bir hesaptır. Hazır değerler hesap grubunu eksiye düşürmüyor ise verilen çekler bu grupta kalabilir. Eksiye düşürmesi halinde 321 Borç Senetleri Hesabına alınmalı. İhtiyari olarak her zaman 321 Hesaba alınabilir; bu durumda 101-Alınan çeklerin 121 Alacak Senetleri Hesabına alınması gerekir.");
            Add(CatDonenVarliklar, "DV-07", "Yeni yıla devir eden stok miktarı/stok tutarı firmanın mutlaka bilgisi olmalı. Devren stok tutarı mutabakat/bilgi dahilinde devir etmeli.");
            Add(CatDonenVarliklar, "DV-08", "Geçici vergi beyanında atlanmış ve geçici vergi sonrası şüpheli duruma düşmüş alacak var mı, karşılık ayrılması gereken alacak var mı?");
            Add(CatDonenVarliklar, "DV-09", "Dönem sonu itibari ile 120-340 / 320-159 hesap kontrolleri yapılmalı. Finansman gider kısıtlamasına tabi olmasa bile rakamların neden avans hesabında olması gerektiği sorgulanmalı ve ona göre avans hesapları alınmalı.");
            Add(CatDonenVarliklar, "DV-10", "Dönem sonu itibari ile 180 hesap kalmamalı, gelecek yıla ait giderler 280 hesapta takip edilmeli. Açılış fişinden sonra 280 hesaplar 180 hesaplara taşınmalı.");
            Add(CatDonenVarliklar, "DV-11", "Devreden KDV mutlaka Aralık beyanı ile kontrol edilmeli. Geçici vergi beyanından sonra düzeltme verilmiş olabilir. Devir var ise 190 hesap ile son KDV beyanı check edilmeli.");

            // 2. Duran Varlık (3)
            Add(CatDuranVarlik, "DUV-01", "Kredi ile alınmış varlık var mı, ilk yıl faiz ve kur farkları maliyet yazılmış mı, kontrol edilmeli. Araç, bina, arsa, makine alımlarında ilk yıl (31.12'ye kadar olan süre) oluşan bu farklar varlıkların maliyetine yazılır.");
            Add(CatDuranVarlik, "DUV-02", "25'li grup ile demirbaş tablosu toplamı 257+268 hesap toplamı demirbaş tablosu eşit mi kontrol edilmeli. Şirket dönem içinde bir adres değişikliği yaptıysa 264-Özel maliyet hesabının bakiyesi ve ona ilişkin 268-Amortisman hesapları kontrol edilerek direkt olarak gider yazılmalıdır.");
            Add(CatDuranVarlik, "DUV-03", "Varlık satışı nedeni ile 549-Özel Fonların durumu kontrol edilmeli. Dönem içinde fona konu varlık varsa amortisman mahsubu ve üç yıl hesabı kontrol edilmelidir.");

            // 3. Kısa V.Y (4)
            Add(CatKisaVY, "KVY-01", "Örnek ortaklar cari hesabına bakılabilir. Ortak hesabı şirkete borçlu ise kar payı ve adat faizi hesaplanma durumu kontrol edilmelidir.");
            Add(CatKisaVY, "KVY-02", "Aralık ayına ait KDV, Muhtasar, GEKAP beyanları 360 hesaplar ile uyumlu mu, ödenmeyen vergiler 368 hesaba alınmış mı kontrol edilmeli.");
            Add(CatKisaVY, "KVY-03", "361 bakiyesi Aralık bildirgesi ile uyumlu mu kontrol edilmeli.");
            Add(CatKisaVY, "KVY-04", "Cari hesap, kredi vb hesapların kur değerlemesi yapılmış mı, varsa finansman gider hesaplaması kapsamında değerlendirilmiş mi?");

            // 4. Bilanço İşlemi (6)
            Add(CatBilancoIslemi, "BIL-01", "Geçici vergi beyanından sonra gelecek yıl için dava açılacak firma var ise kurumları yaparken gelecek yıla doğru devriden olunmalı. Örneğin x firmasının 2025 yılından 2026 yılına devir eden bakiyesi olası alacak davası için uygun mu kontrol edilmelidir.");
            Add(CatBilancoIslemi, "BIL-02", "Şirket bir grup firması ise grup firmaları cari hesapları mutabakatı yapılmalı.");
            Add(CatBilancoIslemi, "BIL-03", "Genel olarak cari hesap kontrolleri yapılmış mı, özellikle PKF Muhasebe ile mutabakat yapılmalı.");
            Add(CatBilancoIslemi, "BIL-04", "Genel mizan kontrolleri neticesinde şirketin sermaye artırımına ihtiyaç var mı?");
            Add(CatBilancoIslemi, "BIL-05", "İlişkili kişiler ile transfer fiyatlandırmasına konu işlem var mı, ilişkili kişilerin kim olduğu Transfer Fiyatlandırması sayfasında mevcuttur.");
            Add(CatBilancoIslemi, "BIL-06", "Şirket bu yıl mı açılmış, sermaye taahhüt kayıtları var mı, sermaye hesapları doğru mu, ortakları doğru açılmış mı?");

            // 5. Gelir Tablosu (3)
            Add(CatGelirTablosu, "GT-01", "Faiz ve fon gelirlerine ilişkin banka yazıları temin edilmeli, kayıtlar ile uyumu 642-645-193 hesaplar ile kontrol edilmeli.");
            Add(CatGelirTablosu, "GT-02", "12 Aylık KDV beyanları toplamı gelir tablosu brüt satışlarına eşit mi, eşit değil ise farklar açıklanabilir mi?");
            Add(CatGelirTablosu, "GT-03", "Kredi faizleri ve banka mevduat faizleri: Taksitli kredilerde faiz giderleri, ödeme tarihine göre ilgili döneme tahakkuk ettirilmelidir. Örneğin ödeme tarihi 10 Ocak ise, 31.12 kapanışı nedeniyle faiz tutarının 20 günlük kısmı içinde bulunulan döneme aittir. Bu durumda 20 günlük faiz için 780 hesaba borç, 381 hesaba alacak kaydı yapılır. Faiz tutarı 50.000 TL ise hesaplama: 50.000 / 30 x 20 = 33.333,33 TL. Aynı uygulama faiz gelirleri için de geçerlidir.");

            // 6. Beyanname Sonuç (16)
            Add(CatBeyannameSonuc, "BEY-01", "Ödenmeyen SGK prim var mı, gider hesaplarından çıkarılıp KKEG yapılmış mı, daha önce KKEG yapılıp bu döneme indirim konusu yapılacak SGK ödemesi var mı?");
            Add(CatBeyannameSonuc, "BEY-02", "Ödenen/ödenmeyen geçici vergilerin durumu kontrol edilmeli. Ödenmeyen geçici vergiler kurumlar beyanında indirim konusu yapılmamalı.");
            Add(CatBeyannameSonuc, "BEY-03", "Kurumlar vergisi iadesi mi çıkıyor, iade çıkıyor ise GEKSİS raporuna uygun kontroller yapılmalı (Brüt satış kontrolleri, ortaklar adat kontrolleri).");
            Add(CatBeyannameSonuc, "BEY-04", "Verilen geçici vergi beyanı sonrası kurumlar vergisi ödemesi çıkıyor mu, %10'luk matrah artışı söz konusu mu?");
            Add(CatBeyannameSonuc, "BEY-05", "Kar zarar değişiyor mu, değişimin nedeni kontrol edilmeli.");
            Add(CatBeyannameSonuc, "BEY-06", "4. Dönem geçiciye göre KKEG değişiyor mu, değişiyor ise nedeni kontrol edilmeli.");
            Add(CatBeyannameSonuc, "BEY-07", "Dönem net karı veya zararı gelir tablosunda ve bilançoda eşit mi? Kar çıkmış ise ödenen vergi gelir tablosundan kardan düşülmüş mü? (K-DÖNEM KARI VERGİ VE DİĞER YASAL YÜKÜMLÜLÜK KARŞILIKLARI (-))");
            Add(CatBeyannameSonuc, "BEY-08", "370 hesap ile ödenen/ödenecek vergi eşit mi, peşin ödenen vergiler (193 HESAP - 371 eşitlenmiş mi?), 370-371 farkı son dönem geçici kadar mı?");
            Add(CatBeyannameSonuc, "BEY-09", "Muhasebe uygulama tebliği uyarınca firmanın net satışları ve aktif toplamı ek mali tablo doldurulmasını zorunlu kılıyor mu? (AKTİF TOP: 240.560.700 TL / NET SATIŞ: 534.574.300 TL)");
            Add(CatBeyannameSonuc, "BEY-10", "7326 / 6111 / 7440 SK matrah artırım kayıtları kontrolü.");
            Add(CatBeyannameSonuc, "BEY-11", "7326 / 6111 / 7440 SK işleminden kaynaklanan 689 hesapta KKEG var mı?");
            Add(CatBeyannameSonuc, "BEY-12", "İstisna işlemi var mı, istisna işlemi kaynaklı zarar var mı, indirimli kurumlar uygulanmış mı? Arsa, daire satışı kaynaklı fon kaydı yapılmış mı, gerekli kurallara uyulmuş mu, yeminli raporuna ihtiyaç var mı?");
            Add(CatBeyannameSonuc, "BEY-13", "Bilanço ve Gelir Tablosu dipnotları doldurulmuş mu?");
            Add(CatBeyannameSonuc, "BEY-14", "Yabancı para pozisyonu dolduruldu mu?");
            Add(CatBeyannameSonuc, "BEY-15", "Örtülü sermaye kontrolü yapıldı mı, ortaklara yürütülen adat var mı, beyannamenin arka sayfasına yazılmış mı? Transfer fiyatlandırmasına konu işlem beyannameye yazılmış mı?");
            Add(CatBeyannameSonuc, "BEY-16", "Beyannameye geriye dönük 5 yıllık geçmiş yıl zararı yazıldı mı? 2025 kurumlar beyanında 24-23-22-21-20 yılları zararı mahsup edilebilir. Matrah artırımı dolayısıyla ilgili yılların zararının tamamı değil %50'si zarar olarak yazılır. Firmanın YMM'si var ise ilgili beyanname bölümünde bu bilgi yazılmış mı? Beyannameye aktarılan önceki yıl bilanço ve gelir tablosu aktarımı doğru mu?");

            // 7. Ticaret Sicil (3)
            Add(CatTicaretSicil, "TS-01", "Şirkette hisse devri, sermaye artırımı yapılmış mı, yapılan bu işlemler şirket kayıtlarına yansımış mı, kayıtlar ve gerçek faydalanıcı bildirimi güncellenmeli, kontrol edilmeli.");
            Add(CatTicaretSicil, "TS-02", "Şirketin adres değişikliği var mı, var ise kurumlar vergisi beyanı hangi vergi dairesine veriliyor kontrol edilmeli. Şube veya depo açılışı var mı, beyana yazılmış mı?");
            Add(CatTicaretSicil, "TS-03", "Nakit sermaye artışı yapılmış mı, ne tutarda yapılmış, nakit sermaye artış indirim hakkı var mı, gerekli dilekçe ve banka dekontları hazırlanıp vergi dairesine sunulmuş mu? Sermaye indirimi ilgili tablo hazırlanmış mı?");

            return items;
        }
    }
}

# Görev: Hesaplamalar → Finansman Gider Kısıtlaması

Hesaplamalar sayfasına ikinci alt sekme ekle: **Finansman Gider Kısıtlaması**
(`/hesaplamalar/finansman-gider-kisitlamasi`).

Bordro sekmesindeki kalıbı izle — `HesaplamaSekmesi` kayıt listesine bir satır,
yeni bir Razor bileşeni, hesaplama mantığı sunucuda. Sayfa iskeletine dokunma.

Mevcut testlerin tamamı aynen geçmeye devam etmeli.

---

## Hesaplama

Dokuz satırlık bir form. Kullanıcı dört alan giriyor, beşi hesaplanıyor.

| # | Satır | Kaynak |
|---|---|---|
| 1 | Özsermaye tutarı | **giriş** (zorunlu) |
| 2 | Yabancı kaynak toplamı (Aktif − Özsermaye) | **giriş** |
| 3 | Özsermayeyi aşan yabancı kaynak tutarı | `2 − 1` |
| 4 | Aşan kısmın yabancı kaynağa oranı | `3 ÷ 2` (yüzde) |
| 5 | Finansman gider tutarı (780, 660, 656 vb.) | **giriş** |
| 6 | Örtülü sermayeye ait finansman gideri (KKEG) / aynı yabancı kaynak nedeniyle elde edilen finansman geliri | **giriş** |
| 7 | Hesaplamada dikkate alınacak finansman gideri | `5 − 6` |
| 8 | Aşan kısma isabet eden finansman gideri | `4 × 7` |
| 9 | **KKEG olacak finansman giderleri** | `8 × kısıtlama oranı` |

### Kenar kuralları — bunlar şart

- **1. satırdaki özsermaye negatifse 0 kabul edilir.** Negatif değerle hesaplama
  yapılmaz.
- **3. satır sıfır veya negatifse gider kısıtlaması yapılmaz** — 4'ten 9'a kadar
  tüm satırlar sıfır döner ve ekranda "yabancı kaynak özsermayeyi aşmıyor, gider
  kısıtlaması yapılmaz" açıklaması gösterilir.
- 2. satır sıfırsa 4. satırda sıfıra bölme olmamalı; sonuç sıfır kabul edilsin.
- 7. satır negatif çıkarsa (finansman geliri giderden fazlaysa) sıfır kabul
  edilsin.

### Kısıtlama oranı parametre olsun

9. satırdaki oran (şu an **%10**) Cumhurbaşkanı Kararı ile belirleniyor ve
değişebilir. Koda gömme — bordro parametrelerinde olduğu gibi **yıl bazlı bir
tabloda** tutulsun ve ekrandan düzenlenebilsin. Kullanıcı hesap yılını seçsin, o
yılın oranı uygulansın.

---

## Ekran

Dokuz satır, TÜRMOB'un sayfasındaki sırayla. Giriş alanları düzenlenebilir,
hesaplananlar salt okunur ve görsel olarak ayrışsın. Değer değiştikçe anlık
hesaplansın.

Sağ tarafta veya alanların altında kısa açıklamalar olsun:

- Özsermayesi yabancı kaynaklarından fazla olan kurumlar finansman gider
  kısıtlaması yapmayacaklardır.
- Örtülü sermayeye isabet eden finansman gideri varsa bu tutar doğrudan KKEG
  olur; toplam finansman giderlerinden bu tutar düşülür, kalan finansman gideri
  kısıtlamaya tabi olur.
- Hesaplanan finansman gider kısıtlaması beyanname üzerinde KKEG olarak
  gösterilir; gelir tablosunda gider yazılan finansman giderleri üzerinde
  herhangi bir işlem yapılmaz.
- İşletmelerin mevduat ve benzeri alacak hesaplarının değerlemesinden kaynaklanan
  kur farkları kısıtlamaya tabi değildir.

Tutarlar Türkçe biçimde (binlik nokta, ondalık virgül), yüzde iki ondalıkla
gösterilsin.

---

## Testler

Hesaplama servisine birim testleri yaz:

- Normal senaryo: yabancı kaynak özsermayeyi aşıyor, KKEG doğru hesaplanıyor
- Yabancı kaynak özsermayeyi aşmıyor → tüm sonuç satırları sıfır
- Özsermaye negatif → sıfır kabul edilip hesaplanıyor
- Yabancı kaynak sıfır → sıfıra bölme yok
- Finansman geliri giderden fazla → 7. satır sıfır
- Seçilen yıl için oran tanımlı değilse anlaşılır hata

---

## Kabul kriterleri

1. Derleme temiz, tüm mevcut testler aynen geçiyor
2. Migration üretildi ve uygulandı, `has-pending-model-changes` temiz
3. Sekme çalışıyor, dokuz satır doğru hesaplanıyor
4. Kenar kuralları test edildi ve geçiyor
5. Kısıtlama oranı tabloda ve ekrandan düzenlenebiliyor
6. Sayfa iskeleti (`HesaplamalarPage.razor`) değişmedi — yalnız sekme kaydı eklendi

Sonunda `OZET.md` ve `KARARLAR.md` güncelle.

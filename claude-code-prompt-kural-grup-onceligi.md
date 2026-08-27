# Görev: Sabit Kuralın Ana Grubu Adayları Önceliklendirsin

Bu görevi baştan sona, soru sormadan tamamla. Önce ilgili kodu oku, sonra değiştir.
Belirsizlik çıkarsa mevcut kalıbı izle, kararını `KARARLAR.md`'ye yaz ve devam et.

Mevcut testlerin tamamı aynen geçmeye devam etmeli.

---

## Sorun

Sabit kural bir ana grup belirlediğinde (`MAAŞ AVANSI → 196`), grup içindeki alt
hesap araması diğer gruplardaki aynı isimli kayıtları da eşit aday sayıyor ve
satır gereksiz yere onaya düşüyor.

Gerçek örnek — `MAAŞ AVANSI ... ÖMER CAN DİZDAR hesabına giden FAST ödemesi`.
Hesap planında üç kayıt var:

```
195 01 O09       Ömer Can Dizdar     (iş avansı)
196 03 25 O04    Ömer Can Dizdar     (maaş avansı)   ← doğru olan
335 01 O09       Ömer Can Dizdar     (personele borçlar)
```

Kural `196` dediği ve o grupta **tam bir tane** aday olduğu için otomatik
seçilmeliydi. Şu an üçü eşit sayılıp satır onaya düşüyor.

---

## Düzeltme

Kural bir ana grup belirlediğinde:

- O grupta **tam bir tane** aday varsa **otomatik seçilsin.** Diğer gruplardaki
  adaylar alternatif olarak gösterilsin ama otomatik çözümü engellemesin.
- O grupta **sıfır** aday varsa satır onaya düşsün; diğer gruplardaki adaylar
  listelensin.
- O grupta **iki veya daha fazla** aday varsa satır onaya düşsün; hepsi
  listelensin.

### Doğrulanmış senaryolar

| Metin | Kural grubu | Grup içi aday | Beklenen |
|---|---|---|---|
| `ÖMER CAN DİZDAR` | 196 | 1 (`196 03 25 O04`) | **Otomatik** → `196 03 25 O04` |
| `EMİRHAN ÖZER` | 196 | 2 (`196 03 25 E01`, `196 IU 77`) | Onaya düşer, ikisi de aday |
| `ABDULKADİR SAYICI` | 195 | 0 (yalnız `331 02` var) | Onaya düşer, `331 02` aday |
| `EMİRHAN ÖZDEMİR` | 196 | 0 (planda yok) | Onaya düşer, öneri yok |

---

## Kuralın birden fazla ana grubu olabilsin

`Avans` gibi genel bir kural hem `195` hem `196` kapsayabilir; şu an sabit kural
tablosunda tek hesap kodu alanı var.

- Kurala **birden fazla ana grup** tanımlanabilsin (virgülle ayrılmış: `195, 196`)
- Bu durumda tanımlı grupların **tamamında toplam tek aday** varsa otomatik
  seçilsin; birden fazla varsa onaya düşsün ve hepsi listelensin
- Mevcut `İş Avansı → 195` ve `Maaş Avansı → 196` kuralları tek gruplu kalsın;
  genel `Avans` kuralı `195, 196` olsun

### Kural sırası

`Maaş Avansı` ve `İş Avansı` kuralları genel `Avans` kuralından **önce** gelmeli
(sıra numarası küçük). Seed'deki sıraların bunu sağladığını doğrula; sağlamıyorsa
düzelt. Sabit Kurallar ekranında sıra kolonunun düzenlenebilir olduğunu da
kontrol et.

---

## Kabul kriterleri

1. Derleme temiz, tüm mevcut testler aynen geçiyor
2. Migration gerekiyorsa üretildi ve uygulandı
3. Yukarıdaki dört senaryo gerçek metinlerle test edildi ve geçiyor
4. Çoklu ana grup tanımlanabiliyor ve doğru çalışıyor
5. Kural sırası doğru; dar ifade genel ifadeden önce

Sonunda `OZET.md` ve `KARARLAR.md` güncelle. Gerçek dosyayla çalıştırıp otomatik /
onay bekleyen / çözülemeyen sayılarının nasıl değiştiğini yaz.

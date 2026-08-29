# Görev: Nginx'e SignalR Hub Rotası Ekle

Bu görevi baştan sona, soru sormadan tamamla. Yalnız nginx yapılandırması
değişecek — kod, test veya başka dosyaya dokunma.

## Sunucudan ölçülen gerçek durum

Aşağıdakiler canlı sunucuda doğrulandı, varsayım değil:

| | |
|---|---|
| Nginx conf dosyası | `Nginx/conf.d/dijitalmasraf.conf` (imaja gömülü, bind mount değil) |
| Nginx imajı | `microserviceproject_nginx.public`, container `c_nginx_public` |
| CatalogService ağ adı | `catalogservice.api`, port `5004`, ağ `net_backendservices` |
| Mevcut `/catalog/` bloğu | 45. satır, `proxy_pass http://web.apigateway:5000/catalog/;` |
| `map $http_upgrade` | **YOK** — eklenecek (grep ile doğrulandı, boş döndü) |

**Kritik:** `/catalog/`, `/auth/` gibi mevcut bloklar trafiği `web.apigateway:5000`
(Ocelot) üzerinden geçiriyor. SignalR hub'ı **gateway'i atlayıp doğrudan**
`catalogservice.api:5004`'e gitmeli — WebSocket uzun ömürlü bağlantı, gateway'in
timeout ve buffering ayarlarıyla iyi geçinmiyor (KARARLAR'da yazılı karar).

## Yapılacak

`Nginx/conf.d/dijitalmasraf.conf` dosyasına iki ekleme:

**1. Dosyanın en başına**, hiçbir `server { }` bloğunun içinde olmayacak şekilde:

```nginx
map $http_upgrade $connection_upgrade {
    default upgrade;
    ''      close;
}
```

**2. `dijitalmasraf.com` server bloğunun içine**, mevcut `location /catalog/`
bloğunun hemen yanına:

```nginx
    # SignalR agent hub — gateway'i atlar, doğrudan CatalogService'e gider
    location /agenthub {
        proxy_pass http://catalogservice.api:5004;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection $connection_upgrade;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_read_timeout 3600s;
        proxy_send_timeout 3600s;
        proxy_buffering off;
    }
```

`proxy_pass` sonunda **yol yazma** (`/agenthub` veya `/` ekleme) — bu haliyle
`/agenthub/negotiate` gibi alt yollar olduğu gibi geçer. Yol eklenirse SignalR
el sıkışması bozulur.

Yalnız `dijitalmasraf.com` server bloğuna ekle; `s3.dijitalmasraf.com` ve
`minio.dijitalmasraf.com` bloklarına dokunma.

## Kontroller

- Dosyada zaten bir `map $http_upgrade` veya `/agenthub` bloğu varsa **ikincisini
  ekleme** — çift tanım nginx'in hiç açılmamasına yol açar. Varsa mevcut olanı
  yukarıdaki içerikle karşılaştır, eksik başlık varsa tamamla.
- `deploy/nginx-agenthub.conf` dosyası önceki turda üretilmişti; içeriğini
  yukarıdakiyle karşılaştır. Hedef adres veya başlıklar farklıysa yukarıdaki
  doğrudur (gerçek sunucudan ölçüldü), o dosyayı da güncelle veya sil.
- Yapılandırmayı sözdizimi açısından doğrula. Docker'a erişimin varsa gerçekten
  `nginx -t` çalıştır; yoksa en azından blok/parantez dengesini kontrol et.

## Sonuç

`OZET.md`'ye yayınlama talimatını yaz — kullanıcı bunu sunucuda elle
çalıştıracak:

```
docker compose build nginx.public
docker compose up -d nginx.public
docker exec c_nginx_public nginx -t
```

`nginx -t` hata verirse reload edilmemeli, aksi halde site tamamen düşer.

Doğrulama adımı olarak da yaz: `https://dijitalmasraf.com/api/catalog/agent/baglilar`
boş liste dönmeli, ve `tools/AgentHubTestClient` prod adresine karşı bağlanmalı.

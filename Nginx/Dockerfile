FROM nginx:stable

# 🧰 Gerekli paketler: dos2unix
RUN apt-get update && apt-get install -y dos2unix

# 🔄 Varsayılan conf dosyasını sil
RUN rm /etc/nginx/conf.d/default.conf

# ⚙️ Yeni site konfig dosyasını kopyala ve satır sonlarını düzelt
#COPY conf.d/dijitalmasraf.conf /etc/nginx/conf.d/default.conf
#RUN dos2unix /etc/nginx/conf.d/default.conf
#
COPY conf.d/dijitalmasraf.conf /etc/nginx/conf.d/dijitalmasraf.conf
COPY conf.d/minio-https.conf   /etc/nginx/conf.d/minio-https.conf
RUN dos2unix /etc/nginx/conf.d/dijitalmasraf.conf /etc/nginx/conf.d/minio-https.conf

# ⚙️ Genel nginx.conf dosyasını kopyala ve satır sonlarını düzelt
COPY nginx.conf /etc/nginx/nginx.conf
RUN dos2unix /etc/nginx/nginx.conf

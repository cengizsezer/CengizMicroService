FROM nginx:stable

# Varsayılan default.conf'u sil (içinde basic welcome sayfası vs. vardır)
RUN rm /etc/nginx/conf.d/default.conf

# Kendi konfigürasyonunu default.conf olarak yerleştir
COPY conf.d/dijitalmasraf.conf /etc/nginx/conf.d/default.conf

# Eğer istersen nginx.conf'u da override edebilirsin (şart değil)
COPY nginx.conf /etc/nginx/nginx.conf

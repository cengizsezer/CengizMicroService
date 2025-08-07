// map.js - KESİN ÇÖZÜM VERSİYONU
console.log("map.js yüklendi"); // Kontrol için

window.DijitalMasraf = window.DijitalMasraf || {};
window.DijitalMasraf.MapFunctions = {
    loadSvgMap: function (dotnetHelper) {
        console.log("loadSvgMap fonksiyonu çağrıldı");

        fetch('/img/TurkeyRegionOnClickMap.svg')
            .then(response => {
                if (!response.ok) throw new Error(`HTTP error! status: ${response.status}`);
                return response.text();
            })
            .then(svgData => {
                const mapContainer = document.getElementById('map');
                if (!mapContainer) throw new Error("Map container bulunamadı");

                mapContainer.innerHTML = svgData;
                console.log("SVG başarıyla yüklendi");

                // Tüm SVG elementlerini seç (path, polygon, rect vb.)
                const interactiveElements = mapContainer.querySelectorAll('path, polygon, rect, circle');
                console.log(`${interactiveElements.length} interaktif element bulundu`);

                interactiveElements.forEach(element => {
                    element.style.cursor = 'pointer';
                    element.addEventListener('click', () => {
                        const regionName = element.id ||
                            element.getAttribute('name') ||
                            element.getAttribute('data-region');
                        console.log(`Tıklanan element:`, element);
                        if (regionName) {
                            dotnetHelper.invokeMethodAsync('OnRegionClicked', regionName);
                        }
                    });
                });
            })
            .catch(error => {
                console.error("SVG yükleme hatası:", error);
            });
    }
};

// Alternatif global erişim
window.loadSvgMap = window.DijitalMasraf.MapFunctions.loadSvgMap;
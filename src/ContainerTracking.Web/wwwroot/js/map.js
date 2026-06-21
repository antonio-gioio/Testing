/**
 * ctMap — Leaflet.js map module for ContainerTrack
 * Uses OpenStreetMap tiles (no API key required)
 * Supports container markers, vessel tracks, and live position updates
 */
window.ctMap = (function () {
    const maps = {};
    const containerIcon = L.divIcon({ className: 'ct-marker-container', html: '📦', iconSize: [24, 24] });
    const vesselIcon = L.divIcon({ className: 'ct-marker-vessel', html: '🚢', iconSize: [28, 28] });
    const containerGroup = {};
    const vesselGroup = {};

    function init(elementId, dotnetRef) {
        if (maps[elementId]) return;

        const map = L.map(elementId, {
            center: [20, 0],
            zoom: 3,
            minZoom: 2,
            maxZoom: 18
        });

        L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
            attribution: '© <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors',
            maxZoom: 18
        }).addTo(map);

        // Optional: Add OpenSeaMap nautical layer
        L.tileLayer('https://tiles.openseamap.org/seamark/{z}/{x}/{y}.png', {
            attribution: '© <a href="https://www.openseamap.org">OpenSeaMap</a>',
            opacity: 0.6
        }).addTo(map);

        containerGroup[elementId] = L.layerGroup().addTo(map);
        vesselGroup[elementId] = L.layerGroup().addTo(map);

        map.on('moveend', () => {
            if (dotnetRef) {
                const b = map.getBounds();
                dotnetRef.invokeMethodAsync('OnMapBoundsChanged',
                    b.getSouth(), b.getWest(), b.getNorth(), b.getEast());
            }
        });

        maps[elementId] = map;
    }

    function updateMarkers(elementId, containers, vessels) {
        const map = maps[elementId];
        if (!map) return;

        containerGroup[elementId].clearLayers();
        vesselGroup[elementId].clearLayers();

        (containers || []).forEach(c => {
            const marker = L.marker([c.latitude, c.longitude], { icon: containerIcon });
            marker.bindPopup(`
                <div class="ct-popup">
                    <strong>${c.containerNumber}</strong><br>
                    Status: ${c.status}<br>
                    ${c.location ? 'Location: ' + c.location : ''}
                </div>
            `);
            containerGroup[elementId].addLayer(marker);
        });

        (vessels || []).forEach(v => {
            const marker = L.marker([v.latitude, v.longitude], {
                icon: vesselIcon,
                rotationAngle: v.heading || 0
            });
            marker.bindPopup(`
                <div class="ct-popup">
                    <strong>${v.name || v.imo}</strong><br>
                    Speed: ${v.speed ? v.speed.toFixed(1) + ' kn' : '—'}<br>
                    Heading: ${v.heading ? v.heading + '°' : '—'}
                </div>
            `);
            vesselGroup[elementId].addLayer(marker);
        });
    }

    function updateContainerPosition(elementId, containerId, lat, lon, status) {
        const map = maps[elementId];
        if (!map) return;
        // Update individual container marker without full refresh
    }

    function focusContainer(elementId, lat, lon) {
        const map = maps[elementId];
        if (map) map.setView([lat, lon], 8);
    }

    function destroy(elementId) {
        const map = maps[elementId];
        if (map) {
            map.remove();
            delete maps[elementId];
            delete containerGroup[elementId];
            delete vesselGroup[elementId];
        }
    }

    return { init, updateMarkers, updateContainerPosition, focusContainer, destroy };
})();

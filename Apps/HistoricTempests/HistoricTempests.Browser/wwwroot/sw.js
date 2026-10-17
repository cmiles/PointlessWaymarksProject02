// Service Worker for HistoricTempests PWA installability
const CACHE_NAME = 'historictempests-pwa-v2';

self.addEventListener('install', (event) => {
    // Pre-cache index.html and essential icons
    event.waitUntil(
        caches.open(CACHE_NAME).then((cache) => {
            return cache.addAll([
                'index.html',
                'manifest.json',
                'icon-192.png',
                'icon-512.png'
            ]);
        }).then(() => self.skipWaiting())
    );
});

self.addEventListener('activate', (event) => {
    event.waitUntil(
        caches.keys().then((keys) => {
            return Promise.all(
                keys.map((key) => {
                    if (key !== CACHE_NAME) {
                        return caches.delete(key);
                    }
                })
            );
        }).then(() => self.clients.claim())
    );
});

self.addEventListener('fetch', (event) => {
    if (event.request.method !== 'GET') return;

    const url = new URL(event.request.url);

    // If navigation request (e.g. launching PWA or opening app)
    if (event.request.mode === 'navigate') {
        // If requesting root/directory without index.html (which 404s on static hosts without directory indexes)
        const isDirectory = url.pathname.endsWith('/') || 
                            url.pathname.endsWith('/HistoricTempests') || 
                            !url.pathname.split('/').pop().includes('.');

        const targetUrl = isDirectory 
            ? new URL('index.html', url.href) 
            : event.request;

        event.respondWith(
            fetch(targetUrl)
                .then((response) => {
                    // If network returned 404 on directory navigation, fetch index.html
                    if (response.status === 404 && targetUrl === event.request) {
                        return fetch(new URL('index.html', url.href));
                    }
                    return response;
                })
                .catch(() => caches.match('index.html'))
        );
        return;
    }

    // Default network-first handler for other assets (ensures latest WASM / data is always loaded)
    event.respondWith(
        fetch(event.request).catch(() => caches.match(event.request))
    );
});

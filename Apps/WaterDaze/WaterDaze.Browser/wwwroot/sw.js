// Service Worker for WaterDaze PWA installability
const CACHE_NAME = 'waterdaze-pwa-v1';

self.addEventListener('install', (event) => {
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
        const isDirectory = url.pathname.endsWith('/') || 
                            url.pathname.endsWith('/WaterDaze') || 
                            !url.pathname.split('/').pop().includes('.');

        const targetUrl = isDirectory 
            ? new URL('index.html', url.href) 
            : event.request;

        event.respondWith(
            fetch(targetUrl)
                .then((response) => {
                    if (response.status === 404 && targetUrl === event.request) {
                        return fetch(new URL('index.html', url.href));
                    }
                    return response;
                })
                .catch(() => caches.match('index.html'))
        );
        return;
    }

    event.respondWith(
        fetch(event.request).catch(() => caches.match(event.request))
    );
});

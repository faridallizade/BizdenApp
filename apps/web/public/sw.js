const CACHE = 'bizden-shell-v1'
const ASSETS = ['/', '/manifest.webmanifest', '/brand/bizden-logo.png']
self.addEventListener('install', event => event.waitUntil(caches.open(CACHE).then(cache => cache.addAll(ASSETS))))
self.addEventListener('activate', event => event.waitUntil(self.clients.claim()))
self.addEventListener('fetch', event => {
  const request = event.request
  if (request.method !== 'GET' || new URL(request.url).pathname.startsWith('/api/')) return
  event.respondWith(fetch(request).then(response => {
    if (response.ok && new URL(request.url).origin === self.location.origin) caches.open(CACHE).then(cache => cache.put(request, response.clone()))
    return response
  }).catch(() => caches.match(request).then(hit => hit || caches.match('/'))))
})

/* global self */
// The console's service worker (#517). It exists so that Chrome and Edge offer to install the
// console as an app with its own window, and it does nothing else: no cache and no fetch handler,
// so every request goes to the server as it would without it. The console is live state, and a
// cached copy of it would be a wrong one.
self.addEventListener("install", () => self.skipWaiting());
self.addEventListener("activate", (event) => event.waitUntil(self.clients.claim()));

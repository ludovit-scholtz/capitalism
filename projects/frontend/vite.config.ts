import { fileURLToPath, URL } from 'node:url'

import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'
import vueDevTools from 'vite-plugin-vue-devtools'
import { VitePWA } from 'vite-plugin-pwa'
import tailwindcss from '@tailwindcss/vite'

// https://vite.dev/config/
export default defineConfig(({ isSsrBuild }) => ({
  plugins: [
    vue(),
    tailwindcss(),
    ...(isSsrBuild ? [] : [vueDevTools()]),
    // PWA plugin is only needed for the client (browser) build.
    ...(isSsrBuild
      ? []
      : [
          VitePWA({
            // We manage service-worker registration ourselves via workbox-window
            // in src/composables/usePwa.ts, so disable auto-injection here.
            registerType: 'prompt',
            injectRegister: null,
            // injectManifest mode: we supply a custom src/sw.ts that workbox
            // compiles and injects the precache manifest into.  This is
            // required because our GraphQL caching strategy uses IDB (POST
            // responses cannot be stored in the Cache Storage API, which only
            // supports GET responses).
            strategies: 'injectManifest',
            srcDir: 'src',
            filename: 'sw.ts',
            injectManifest: {
              // Glob patterns for assets to precache (app shell).
              globPatterns: ['**/*.{js,css,html,ico,svg,woff2}'],
            },
            manifest: {
              name: 'Capitalism 5',
              short_name: 'Capitalism',
              description: 'A business simulation game. Build a business empire and become a tycoon!',
              theme_color: '#0047FF',
              background_color: '#0D1117',
              display: 'standalone',
              start_url: '/',
              scope: '/',
              lang: 'en',
              icons: [
                {
                  src: '/android-chrome-192x192.png',
                  sizes: '192x192',
                  type: 'image/png',
                  purpose: 'any',
                },
                {
                  src: '/android-chrome-512x512.png',
                  sizes: '512x512',
                  type: 'image/png',
                  purpose: 'any maskable',
                },
              ],
            },
          }),
        ]),
  ],
  resolve: {
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url)),
    },
  },
  build: {
    emptyOutDir: true,
    outDir: isSsrBuild ? 'dist/server' : 'dist',
  },
  test: {
    environment: 'node',
    include: ['src/**/*.{test,spec}.ts'],
  },
}))

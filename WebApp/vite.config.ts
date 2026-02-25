import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import { VitePWA } from 'vite-plugin-pwa'

export default defineConfig({
  plugins: [
    react(),
    VitePWA({
      registerType: 'autoUpdate',
      manifest: {
        name: '蘇打石器 GM 後台',
        short_name: 'SodaGM',
        description: '石器時代私服 GM 管理工具',
        theme_color: '#1a1b26',
        background_color: '#1a1b26',
        display: 'standalone',
        icons: [],
      },
    }),
  ],
  server: {
    proxy: {
      '/api': 'http://localhost:5050',
    },
  },
})

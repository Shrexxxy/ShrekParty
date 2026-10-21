import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

const backend = 'http://localhost:5000'

export default defineConfig({
  plugins: [react()],
  server: {
    // host: true — чтобы открыть с телефона в той же Wi-Fi сети.
    host: true,
    proxy: {
      '/api': backend,
      '/media': backend,
      '/hub': { target: backend, ws: true },
    },
  },
  build: {
    outDir: '../src/ShrekParty.Api/wwwroot',
    emptyOutDir: true,
  },
})

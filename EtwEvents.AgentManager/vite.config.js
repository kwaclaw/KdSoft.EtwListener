import { defineConfig } from 'vite';

// https://vitejs.dev/config/
export default defineConfig({
  root: 'src',
  server: {
    port: 41000,
    hmr: {
      host: 'localhost',
      protocol: 'ws'
    },
    cors: true
  },
  build: {
    outDir: '../wwwroot',
    emptyOutDir: true,
    manifest: true,
    rollupOptions: {
      input: {
        main: 'src/build.js'
      }
    },
  }
})

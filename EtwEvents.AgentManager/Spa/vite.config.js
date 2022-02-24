/*eslint-disable */

import { defineConfig } from 'vite';

// https://vitejs.dev/config/
export default defineConfig({
  root: '.',
  server: {
    port: 41000,
    hmr: {
      host: 'localhost',
      protocol: 'ws'
    },
    cors: true
  },
  build: {
    target: 'esnext',
    outDir: '../wwwroot',
    emptyOutDir: true,
    manifest: true,
    rollupOptions: {
      input: {
        main: 'build.js'
      }
    },
  }
});

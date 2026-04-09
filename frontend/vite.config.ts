import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'
import path from 'path'

// https://vite.dev/config/
export default defineConfig({
  plugins: [vue()],
  // Build output to wwwroot for ASP.NET Core static file serving
  base: '/',
  build: {
    outDir: path.resolve(__dirname, '../ChronoCode/wwwroot'),
    emptyOutDir: true,
  },
})

import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'

// https://vite.dev/config/
export default defineConfig({
  // Tailwind CSS v4 is wired in via its dedicated Vite plugin (no postcss config needed).
  plugins: [react(), tailwindcss()],
  server: {
    port: 5173,
  },
})

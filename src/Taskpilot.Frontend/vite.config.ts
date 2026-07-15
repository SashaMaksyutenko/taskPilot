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
  // Vitest reads `test` at runtime; it isn't part of Vite's own config type (and pulling
  // in vitest/config here clashes with Vite 8's rolldown types), so silence the checker.
  // @ts-expect-error -- vitest config field
  test: {
    // Component/hook tests run against a DOM.
    environment: 'jsdom',
    globals: true,
    setupFiles: ['./src/test/setup.ts'],
  },
})

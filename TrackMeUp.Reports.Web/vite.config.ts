import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'
import vuetify from 'vite-plugin-vuetify'

export default defineConfig({
  base: './',
  plugins: [
    vue(),
    vuetify({ autoImport: true }),
    {
      name: 'normalize-index-newlines',
      transformIndexHtml: {
        order: 'post',
        handler: (html) => html.replace(/\r\n?/g, '\n'),
      },
    },
  ],
  build: {
    outDir: 'dist',
    emptyOutDir: true,
    sourcemap: false,
    target: 'es2022',
  },
})

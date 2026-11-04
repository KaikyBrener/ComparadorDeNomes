import { fileURLToPath } from 'node:url';
import { defineConfig } from 'vitest/config';

const aqui = (arquivo: string) => fileURLToPath(new URL(arquivo, import.meta.url));

// A API (ASP.NET Core) responde em http://localhost:5000 durante o desenvolvimento
const API = 'http://localhost:5000';

export default defineConfig({
  build: {
    // O back-end serve o front compilado a partir de wwwroot/
    outDir: '../wwwroot',
    emptyOutDir: true,
    // Nada de data: URI nos assets (a CSP só libera data: para imagens; fontes ficam como arquivos)
    assetsInlineLimit: 0,
    rollupOptions: {
      input: {
        index: aqui('index.html'),
        limpeza: aqui('limpeza.html'),
        historico: aqui('historico.html'),
      },
    },
  },
  css: {
    preprocessorOptions: {
      scss: {
        // O Bootstrap 5.3 ainda usa @import/funções globais do Sass: silencia só esses avisos
        quietDeps: true,
        silenceDeprecations: [
          'import',
          'global-builtin',
          'color-functions',
          'if-function',
          'mixed-decls',
        ],
      },
    },
  },
  server: {
    proxy: {
      '/comparar': API,
      '/limpar': API,
      '/health': API,
    },
  },
  test: {
    environment: 'jsdom',
    include: ['src/**/*.test.ts'],
  },
});

import js from '@eslint/js';
import prettier from 'eslint-config-prettier';
import globals from 'globals';
import tseslint from 'typescript-eslint';

export default tseslint.config(
  { ignores: ['node_modules', 'dist', '../wwwroot'] },
  js.configs.recommended,
  ...tseslint.configs.recommended,
  prettier,
  {
    languageOptions: { globals: { ...globals.browser, ...globals.node } },
    rules: {
      // Dados do usuário nunca viram HTML: use dom.ts (createElement/textContent)
      'no-restricted-properties': [
        'error',
        { property: 'innerHTML', message: 'Use dom.ts (el/textContent): nunca innerHTML.' },
        { property: 'outerHTML', message: 'Use dom.ts (el/textContent): nunca outerHTML.' },
        { property: 'insertAdjacentHTML', message: 'Use dom.ts (el/textContent).' },
      ],
    },
  },
);

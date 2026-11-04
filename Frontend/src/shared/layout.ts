import { el, porId } from './dom';

export type PaginaAtiva = 'comparador' | 'limpeza' | 'historico';

const LINKS: ReadonlyArray<{ pagina: PaginaAtiva; href: string; icone: string; rotulo: string }> = [
  { pagina: 'comparador', href: '/', icone: 'bi-search', rotulo: 'Comparador' },
  { pagina: 'limpeza', href: '/limpeza.html', icone: 'bi-eraser', rotulo: 'Limpeza' },
  { pagina: 'historico', href: '/historico.html', icone: 'bi-clock-history', rotulo: 'Histórico' },
];

/** Monta a barra de navegação dentro de #navbar, destacando o link da página atual. */
export function montarNavbar(ativa: PaginaAtiva): void {
  const destino = porId('navbar');

  const itens = LINKS.map(({ pagina, href, icone, rotulo }) => {
    const atual = pagina === ativa;
    return el(
      'li',
      { classe: 'nav-item' },
      el(
        'a',
        {
          classe: atual ? 'nav-link active' : 'nav-link',
          atributos: { href, ...(atual ? { 'aria-current': 'page' } : {}) },
        },
        el('i', { classe: `bi ${icone} me-1`, atributos: { 'aria-hidden': 'true' } }),
        rotulo,
      ),
    );
  });

  const navbar = el(
    'nav',
    { classe: 'navbar navbar-expand-md app-navbar', atributos: { 'aria-label': 'Principal' } },
    el(
      'div',
      { classe: 'container' },
      el(
        'a',
        { classe: 'navbar-brand', atributos: { href: '/' } },
        el('i', { classe: 'bi bi-search me-2', atributos: { 'aria-hidden': 'true' } }),
        'Comparador de Nomes',
      ),
      el(
        'button',
        {
          classe: 'navbar-toggler',
          atributos: {
            type: 'button',
            'data-bs-toggle': 'collapse',
            'data-bs-target': '#menuPrincipal',
            'aria-controls': 'menuPrincipal',
            'aria-expanded': 'false',
            'aria-label': 'Alternar menu',
          },
        },
        el('span', { classe: 'navbar-toggler-icon' }),
      ),
      el(
        'div',
        { classe: 'collapse navbar-collapse', atributos: { id: 'menuPrincipal' } },
        el('ul', { classe: 'navbar-nav ms-auto' }, ...itens),
      ),
    ),
  );

  destino.replaceChildren(navbar);
}

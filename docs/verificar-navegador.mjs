// Verificação automatizada no navegador (Chrome/Edge via puppeteer-core). Complementa docs/verificacao-manual.md.
//
//   node verificar-navegador.mjs [URL_BASE] [PASTA_DAS_CAPTURAS]
//
// Variáveis de ambiente:
//   CHROME_PATH  caminho do chrome/chromium/msedge (obrigatório se não estiver no local padrão do Windows/Linux/macOS)
//
// Este arquivo NÃO faz parte do build: copie-o para uma pasta vazia, rode `npm i puppeteer-core` lá e execute (veja o roteiro).
// Sai com código 1 se alguma verificação falhar.
import fs from 'node:fs';
import puppeteer from 'puppeteer-core';

const BASE = (process.argv[2] ?? 'http://localhost:5000').replace(/\/$/, '');
const PASTA_CAPTURAS = process.argv[3] ?? null;

const CANDIDATOS_CHROME = [
  process.env.CHROME_PATH,
  'C:/Program Files/Google/Chrome/Application/chrome.exe',
  'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe',
  '/usr/bin/google-chrome',
  '/usr/bin/chromium',
  '/usr/bin/chromium-browser',
  '/Applications/Google Chrome.app/Contents/MacOS/Google Chrome',
];
const executablePath = CANDIDATOS_CHROME.find((c) => c && fs.existsSync(c));
if (!executablePath) {
  console.error('Chrome não encontrado. Defina CHROME_PATH.');
  process.exit(2);
}

if (PASTA_CAPTURAS) fs.mkdirSync(PASTA_CAPTURAS, { recursive: true });

let falhas = 0;
function conferir(descricao, ok, detalhe = '') {
  console.log(`${ok ? 'OK    ' : 'FALHA '} ${descricao}${detalhe ? ` -> ${detalhe}` : ''}`);
  if (!ok) falhas++;
}

const browser = await puppeteer.launch({ executablePath, headless: true, args: ['--no-sandbox'] });
const pagina = await browser.newPage();
await browser
  .defaultBrowserContext()
  .overridePermissions(BASE, ['clipboard-read', 'clipboard-write', 'clipboard-sanitized-write']);
await pagina.setViewport({ width: 360, height: 800 });

// Tudo que o navegador reclamar (console, erros de página, rede, CSP) é acumulado aqui
const problemas = [];
const origensExternas = new Set();
pagina.on('console', (m) => {
  if (['error', 'warning'].includes(m.type())) problemas.push(`console.${m.type()}: ${m.text()}`);
});
pagina.on('pageerror', (e) => problemas.push(`pageerror: ${e.message}`));
pagina.on('requestfailed', (r) => problemas.push(`requestfailed: ${r.url()}`));
pagina.on('request', (r) => {
  const url = r.url();
  if (!url.startsWith(BASE) && !url.startsWith('data:') && !url.startsWith('blob:')) origensExternas.add(url);
});
// Violações de CSP chegam como eventos "securitypolicyviolation" (a cada navegação)
await pagina.evaluateOnNewDocument(() => {
  document.addEventListener('securitypolicyviolation', (e) => {
    console.error(`CSP: ${e.violatedDirective} bloqueou ${e.blockedURI}`);
  });
});

const semRolagemHorizontal = () =>
  pagina.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth);
const capturar = async (nome, opcoes = {}) => {
  if (PASTA_CAPTURAS) await pagina.screenshot({ path: `${PASTA_CAPTURAS}/${nome}.png`, ...opcoes });
};

// ---------- Comparador ----------
await pagina.goto(`${BASE}/`, { waitUntil: 'networkidle0' });
conferir('Comparador: título da página', (await pagina.title()).startsWith('Comparador de Nomes'), await pagina.title());
conferir('Comparador: sem rolagem horizontal em 360 px', await semRolagemHorizontal());
await capturar('01-comparador-360', { fullPage: true });

// A segunda lista traz um nome com HTML/JS: precisa aparecer como texto, sem executar nada
await pagina.type('#sistema', 'Mariana Silva\nJoão Silva\nJosé Souza\nAna Maria Souza');
await pagina.type(
  '#control',
  'Mariana Silvia\nSilva João\nCarlos <img src=x onerror=window.__xss=1>\njose  souza\nAna M. Souza\nPedro Alves',
);
await pagina.click('#btnComparar');
await pagina.waitForFunction(
  () => document.querySelector('.swal2-title')?.textContent === 'Comparação Concluída!',
);
await capturar('02-resumo-360');
await pagina.click('.swal2-confirm');
await pagina.waitForSelector('.swal2-popup', { hidden: true });

const remover = await pagina.$eval('#resultadoRemover', (e) => e.value);
conferir('Resultado: nome com <img onerror> aparece como texto em "Para Remover"', remover.includes('<img src=x onerror=window.__xss=1>'));
conferir('XSS: o código do nome NÃO foi executado', !(await pagina.evaluate(() => window.__xss === 1)));
conferir('XSS: nenhum <img> foi injetado na página', (await pagina.$$('main img')).length === 0);
conferir('Resultado: "Pedro Alves" também será removido', remover.includes('Pedro Alves'));
conferir('Resultado: "Mariana Silvia" NÃO está na lista de remoção (é similar)', !remover.includes('Mariana Silvia'));
conferir('Resultado: 360 px sem rolagem horizontal', await semRolagemHorizontal());

await pagina.click('#abaSimilares');
await pagina.waitForSelector('#painelSimilares.show.active');
const similares = await pagina.$$eval('#corpoSimilares tr', (trs) => trs.map((tr) => [...tr.cells].map((c) => c.textContent)));
conferir('Similaridades: lista preenchida', similares.length >= 2, JSON.stringify(similares));
await capturar('03-similares-360', { fullPage: true });

await pagina.click('#abaRemover');
await pagina.waitForSelector('#painelRemover.show.active');
await pagina.click('#btnCopiarRemover');
await pagina.waitForSelector('.swal2-popup', { visible: true });
// (no Windows o Chrome devolve a área de transferência com \r\n; por isso a quebra de linha é normalizada)
const copiado = (await pagina.evaluate(() => navigator.clipboard.readText())).replace(/\r\n/g, '\n');
conferir('Copiar: texto na área de transferência é o mesmo da lista', copiado === remover);
await pagina.waitForSelector('.swal2-popup', { hidden: true });

// ---------- Limpeza ----------
await pagina.goto(`${BASE}/limpeza.html`, { waitUntil: 'networkidle0' });
await pagina.select('#tipoFabricante', 'control');
await pagina.type('#dadosLimpeza', '| 1 | Ana Lima |\n| 2 | Bruno Costa |\n| 3 | ana lima |');
await pagina.click('#btnLimpar');
await pagina.waitForFunction(() => document.getElementById('resultadoLimpeza').value !== '');
conferir('Limpeza: nomes extraídos, sem duplicata', (await pagina.$eval('#resultadoLimpeza', (e) => e.value)) === 'Ana Lima\nBruno Costa');
conferir('Limpeza: 360 px sem rolagem horizontal', await semRolagemHorizontal());
await capturar('04-limpeza-360', { fullPage: true });

// ---------- Histórico ----------
await pagina.goto(`${BASE}/historico.html`, { waitUntil: 'networkidle0' });
const nomes = await pagina.$$eval('#corpoHistorico tr', (trs) => trs.map((tr) => tr.cells[0].textContent));
conferir('Histórico: registros da comparação aparecem', nomes.length >= 2, String(nomes.length));
conferir('Histórico: o nome malicioso é texto, não elemento', nomes.some((n) => n.includes('<img')) && (await pagina.$$('#corpoHistorico img')).length === 0);
conferir('Histórico: 360 px sem rolagem horizontal', await semRolagemHorizontal());
await capturar('05-historico-360', { fullPage: true });

// JSON corrompido no localStorage não pode quebrar a página
await pagina.evaluate(() => localStorage.setItem('comparador.historico.v1', '{lixo'));
await pagina.reload({ waitUntil: 'networkidle0' });
conferir('Histórico: JSON corrompido é ignorado (tabela vazia)', (await pagina.$$eval('#corpoHistorico tr td[colspan]', (x) => x.length)) === 1 || (await pagina.$eval('#contagemHistorico', (e) => e.textContent.trim())) === '0');

// Menu no celular
await pagina.click('.navbar-toggler');
await new Promise((r) => setTimeout(r, 500));
conferir('Menu do celular abre', await pagina.$eval('#menuPrincipal', (e) => e.classList.contains('show')));
await capturar('06-menu-360');

// Desktop
await pagina.setViewport({ width: 1280, height: 900 });
await pagina.goto(`${BASE}/`, { waitUntil: 'networkidle0' });
conferir('Desktop (1280 px): sem rolagem horizontal', await semRolagemHorizontal());
await capturar('07-comparador-1280');

// ---------- Resumo global ----------
conferir('Nenhuma requisição a origem externa (sem CDN)', origensExternas.size === 0, [...origensExternas].join(', '));
conferir('Nenhum erro/aviso de console, violação de CSP ou falha de rede', problemas.length === 0, problemas.join(' | '));

await browser.close();
console.log(falhas === 0 ? '\nTudo certo.' : `\n${falhas} verificação(ões) falharam.`);
process.exit(falhas === 0 ? 0 : 1);

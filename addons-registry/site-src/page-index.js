// Script de la page index.html (séparé de la page : la politique de sécurité interdit les scripts en ligne).
import { PERMISSION_TEXT } from './addon-rules.js';
import { initAuthNav } from './auth-client.js';

initAuthNav();

const list = document.getElementById('list');
const search = document.getElementById('search');
let addons = [];

function el(tag, attrs = {}, children = []) {
  const e = document.createElement(tag);
  for (const [k, v] of Object.entries(attrs)) { if (k === 'text') e.textContent = v; else e.setAttribute(k, v); }
  children.forEach((c) => e.appendChild(c));
  return e;
}

function card(a) {
  const isScript = a.permissions.includes('overlay.script');
  const meta = el('div', { class: 'meta' }, [
    el('span', { class: 'badge ' + (a.tier === 'verified' ? 'verified' : 'community'), text: a.tier === 'verified' ? '✓ Vérifié' : 'Communauté' }),
    el('span', { class: 'badge', text: 'v' + a.version }),
    ...(a.author ? [el('span', { class: 'badge author', text: 'par ' + a.author })] : []),
    ...(isScript ? [el('span', { class: 'badge script', text: 'Script' })] : []),
  ]);
  const perms = el('ul', {}, a.permissions.map((p) => el('li', { text: PERMISSION_TEXT[p] || p })));
  const size = (a.size / 1024).toFixed(1).replace('.', ',') + ' Ko';
  const download = el('a', { class: 'btn small', href: a.url, download: `${a.id}-${a.version}.zip`, text: 'Télécharger le .zip · ' + size });
  return el('article', { class: 'addon' }, [
    el('h3', { text: a.name }), meta,
    el('p', { text: a.description || 'Pas de description.' }), perms,
    el('div', { class: 'actions' }, [download]),
  ]);
}

function render() {
  const q = search.value.trim().toLowerCase();
  const shown = addons.filter((a) => !q || (a.name + ' ' + a.id + ' ' + (a.author || '') + ' ' + (a.description || '')).toLowerCase().includes(q));
  list.textContent = '';
  if (shown.length === 0) list.appendChild(el('p', { class: 'empty', text: q ? 'Aucun add-on ne correspond.' : 'Le catalogue est vide.' }));
  shown.forEach((a) => list.appendChild(card(a)));
  document.getElementById('count').textContent = `${shown.length} add-on${shown.length > 1 ? 's' : ''}`;
}

search.addEventListener('input', render);
try {
  const response = await fetch('index.json', { cache: 'no-store' });
  const index = await response.json();
  const revoked = new Set(index.revoked || []);
  addons = (index.addons || [])
    .filter((a) => !revoked.has(a.id) && !revoked.has(`${a.id}@${a.version}`))
    .sort((x, y) => (x.tier === y.tier ? x.name.localeCompare(y.name, 'fr') : x.tier === 'verified' ? -1 : 1));
  render();
} catch {
  list.textContent = '';
  list.appendChild(el('p', { class: 'empty', text: 'Impossible de charger le catalogue. Réessaie plus tard.' }));
}

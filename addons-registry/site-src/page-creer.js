// Script de la page creer.html (séparé de la page : la politique de sécurité interdit les scripts en ligne).
import { validateManifest, makeZip, METRICS, SCOPES } from './addon-rules.js';
import { initAuthNav } from './auth-client.js';

let session = { authenticated: false, user: null };

const METRIC_TEXT = { chests: 'Total (coffres + canons)', opened: 'Coffres ouverts', destroyed: 'Coffres cassés', sprites: 'Coffres d’esprit', buried: 'Coffres enterrés',
                      wanted: 'Cartes Wanted', cannons: 'Canons à butin', treasures: 'Trésors', matches: 'Parties' };
const SCOPE_TEXT = { total: 'Toutes les parties', session: 'Session', last: 'Dernière partie' };
const THEME = [['background', 'Fond', '#090C12'], ['border', 'Bordure', '#242933'], ['line', 'Séparateurs', '#1A1E27'],
               ['muted', 'Textes secondaires', '#94A3B8'], ['text', 'Texte principal', '#F8FAFC'],
               ['accent', 'Total et badge', '#FFC860'], ['treasure', 'Ligne Trésors', '#C4A0FF']];

const $ = (id) => document.getElementById(id);
function el(tag, attrs = {}, children = []) {
  const e = document.createElement(tag);
  for (const [k, v] of Object.entries(attrs)) { if (k === 'text') e.textContent = v; else e.setAttribute(k, v); }
  children.forEach((c) => e.appendChild(c));
  return e;
}
function select(values, labels, value) {
  const s = el('select');
  values.forEach((v) => { const o = el('option', { value: v, text: labels[v] }); if (v === value) o.selected = true; s.appendChild(o); });
  return s;
}
const field = (label, control) => el('div', {}, [el('label', { class: 'field-label', text: label }), control]);

let touched = false;
const counterRows = $('counterRows');
function addCounter() {
  const label = el('input', { type: 'text', maxlength: '24', placeholder: 'Coffres session', autocomplete: 'off' });
  const metric = select(METRICS, METRIC_TEXT, 'chests');
  const scope = select(SCOPES, SCOPE_TEXT, 'session');
  const remove = el('button', { type: 'button', class: 'btn small', text: 'Retirer' });
  const last = el('input', { type: 'checkbox' });
  const row = el('div', {}, [
    el('div', { class: 'grid3' }, [field('Texte de la ligne', label), field('Ce qu’on compte', metric), field('Sur', scope), remove]),
    el('label', { style: 'display:flex;gap:8px;align-items:center;color:var(--soft);font-size:.88rem;margin:-2px 0 14px;cursor:pointer' },
      [last, document.createTextNode('Ajouter « · n dernière partie » (avec « Toutes les parties »)')]),
  ]);
  row.get = () => ({ label: label.value.trim(), metric: metric.value, scope: scope.value, showLast: last.checked });
  remove.addEventListener('click', () => { row.remove(); touched = true; refresh(); });
  row.addEventListener('input', () => { touched = true; refresh(); });
  row.addEventListener('change', () => { touched = true; refresh(); });
  counterRows.appendChild(row);
}

const themeInputs = {};
for (const [key, label, color] of THEME) {
  const use = el('input', { type: 'checkbox', 'aria-label': 'Changer ' + label });
  const picker = el('input', { type: 'color' });
  picker.value = color.toLowerCase();
  const box = el('div', { class: 'color' }, [use, el('span', { text: label }), picker]);
  themeInputs[key] = { use, picker };
  box.addEventListener('input', (ev) => { if (ev.target === picker) use.checked = true; touched = true; refresh(); });
  box.addEventListener('change', () => { touched = true; refresh(); });
  $('themeFields').appendChild(box);
}

function build() {
  const m = { schema: 1, id: $('f-id').value.trim(), name: $('f-name').value.trim(), version: $('f-version').value.trim(),
              description: $('f-desc').value.trim(), permissions: [] };
  const counters = $('useCounters').checked, theme = $('useTheme').checked, script = $('useScript').checked;
  if (counters || script) m.permissions.push('stats.read');
  if (counters) m.permissions.push('overlay.rows');
  if (theme) m.permissions.push('overlay.theme');
  if (script) m.permissions.push('overlay.script');
  if (counters) m.counters = [...counterRows.children].map((r) => r.get());
  if (theme) {
    m.theme = {};
    for (const [key] of THEME) if (themeInputs[key].use.checked) m.theme[key] = themeInputs[key].picker.value.toUpperCase();
  }
  return m;
}

let current = null;
function updateAuthBanner() {
  const text = $('authBannerText');
  const actions = $('authBannerActions');
  text.textContent = '';
  actions.textContent = '';
  if (session.authenticated && session.user) {
    text.append('Connecté en tant que ', el('strong', { text: session.user.username }), ' — tu peux poster directement ton add-on.');
    actions.appendChild(el('a', { class: 'btn small', href: 'connexion.html', text: 'Mon compte' }));
  } else {
    text.textContent = 'Tu peux créer et télécharger le .zip librement. Pour le poster dans le catalogue, connecte-toi.';
    actions.appendChild(el('a', { class: 'btn small primary', href: 'connexion.html?retour=creer.html', text: 'Se connecter' }));
    actions.appendChild(el('a', { class: 'btn small', href: 'connexion.html?mode=inscription&retour=creer.html', style: 'margin-left:6px', text: 'Créer un compte' }));
  }
  $('btnPost').disabled = !current || !session.authenticated;
}

function refresh() {
  $('countersBox').classList.toggle('hidden', !$('useCounters').checked);
  $('themeBox').classList.toggle('hidden', !$('useTheme').checked);
  $('scriptBox').classList.toggle('hidden', !$('useScript').checked);
  const m = build();
  const script = $('useScript').checked ? $('f-script').value.replace(/\r\n/g, '\n') : null;
  const json = JSON.stringify(m, null, 2) + '\n';
  const errors = validateManifest(m, script);
  if (script !== null && new Blob([script]).size > 32 * 1024) errors.push('script.js : 32 Ko au maximum.');
  $('out').textContent = json;
  $('errors').textContent = '';
  if (touched) errors.forEach((msg) => $('errors').appendChild(el('li', { text: msg })));
  const valid = errors.length === 0;
  current = valid ? { m, json, script } : null;
  $('okMsg').textContent = valid
    ? (session.authenticated ? 'Add-on valide : tu peux le télécharger pour l’essayer, ou le poster.' : 'Add-on valide : tu peux télécharger le .zip, ou te connecter pour le poster.')
    : (touched ? '' : 'Remplis les champs ci-dessus : l’add-on se vérifie en direct.');
  $('btnPost').disabled = !valid || !session.authenticated;
  $('btnZip').disabled = !valid;
}

function zipOf(c) {
  const files = [{ name: 'manifest.json', text: c.json }];
  if (c.script !== null) files.push({ name: 'script.js', text: c.script.endsWith('\n') ? c.script : c.script + '\n' });
  return makeZip(files);
}

$('btnZip').addEventListener('click', () => {
  if (!current) return;
  const url = URL.createObjectURL(zipOf(current));
  const a = el('a', { href: url, download: `${current.m.id}-${current.m.version}.zip` });
  document.body.appendChild(a); a.click(); a.remove(); URL.revokeObjectURL(url);
});

$('btnPost').addEventListener('click', async () => {
  if (!current) return;
  $('btnPost').disabled = true;
  $('status').className = 'ok';
  $('status').textContent = 'Envoi en cours…';
  const form = new FormData();
  form.append('zip', zipOf(current), `${current.m.id}-${current.m.version}.zip`);
  form.append('site_web', $('site_web').value);
  try {
    const response = await fetch('api/proposer', { method: 'POST', body: form });
    const data = await response.json().catch(() => ({}));
    if (!response.ok) {
      if (response.status === 401) {
        session = { authenticated: false, user: null };
        updateAuthBanner();
      }
      $('status').textContent = (data.error || 'L’envoi a échoué.') + (data.details ? ' ' + data.details.join(' ') : '');
      $('btnPost').disabled = !session.authenticated;
      return;
    }
    $('status').className = 'ok success';
    $('status').textContent = '';
    $('status').append(`Merci ${session.user?.username || ''} ! Ta proposition est envoyée. Le mainteneur va la relire. `,
      el('a', { href: data.url, target: '_blank', rel: 'noopener', text: 'Voir la proposition' }));
  } catch {
    $('status').textContent = 'Impossible de joindre le site. Vérifie ta connexion et réessaie.';
    $('btnPost').disabled = !session.authenticated;
  }
});

$('addCounter').addEventListener('click', () => { if (counterRows.children.length < 8) addCounter(); touched = true; refresh(); });
for (const id of ['f-id', 'f-name', 'f-version', 'f-desc', 'useCounters', 'useTheme', 'useScript', 'f-script']) {
  $(id).addEventListener('input', () => { touched = true; refresh(); });
  $(id).addEventListener('change', () => { touched = true; refresh(); });
}
addCounter();
refresh();
session = await initAuthNav();
updateAuthBanner();
refresh();

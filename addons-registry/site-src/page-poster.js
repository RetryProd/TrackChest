// Script de la page poster.html (séparé de la page : la politique de sécurité interdit les scripts en ligne).
import { readAddonZip, PERMISSION_TEXT } from './addon-rules.js';
import { initAuthNav } from './auth-client.js';

const $ = (id) => document.getElementById(id);
let current = null;
let session = { authenticated: false, user: null };

function el(tag, attrs = {}, children = []) {
  const e = document.createElement(tag);
  for (const [k, v] of Object.entries(attrs)) { if (k === 'text') e.textContent = v; else e.setAttribute(k, v); }
  children.forEach((c) => e.appendChild(typeof c === 'string' ? document.createTextNode(c) : c));
  return e;
}

function updateAuthBanner() {
  const text = $('authBannerText');
  const actions = $('authBannerActions');
  text.textContent = '';
  actions.textContent = '';
  if (session.authenticated && session.user) {
    text.append('Connecté en tant que ', el('strong', { text: session.user.username }), ' — ton pseudo sera associé à la proposition.');
    actions.appendChild(el('a', { class: 'btn small', href: 'connexion.html', text: 'Mon compte' }));
  } else {
    text.textContent = 'Connecte-toi avec un compte TrackChest pour poster ton add-on et l’associer à ton pseudo.';
    actions.appendChild(el('a', { class: 'btn small primary', href: 'connexion.html?retour=poster.html', text: 'Se connecter' }));
    actions.appendChild(el('a', { class: 'btn small', href: 'connexion.html?mode=inscription&retour=poster.html', style: 'margin-left:6px', text: 'Créer un compte' }));
  }
  $('send').disabled = !current || !session.authenticated;
}

function show(title, rows, errors) {
  $('result').classList.remove('hidden');
  $('resultTitle').textContent = title;
  $('summary').textContent = '';
  rows.forEach(([k, v]) => { $('summary').appendChild(el('dt', { text: k })); $('summary').appendChild(el('dd', { text: v })); });
  $('errors').textContent = '';
  errors.forEach((msg) => $('errors').appendChild(el('li', { text: msg })));
}

async function choose(file) {
  current = null;
  $('send').disabled = true;
  $('status').textContent = '';
  $('status').className = 'ok';
  $('reset').classList.remove('hidden');
  if (!file) return;
  if (file.size > 64 * 1024) { show('Fichier refusé', [['Fichier', file.name]], ['Le .zip fait plus de 64 Ko.']); return; }
  const addon = await readAddonZip(await file.arrayBuffer());
  const m = addon.manifest;
  const rows = [['Fichier', `${file.name} (${(file.size / 1024).toFixed(1).replace('.', ',')} Ko)`]];
  if (m && typeof m === 'object') {
    rows.push(['Add-on', `${m.name ?? '?'} (${m.id ?? '?'}) ${m.version ?? ''}`]);
    if (m.description) rows.push(['Description', m.description]);
    if (Array.isArray(m.permissions)) rows.push(['Permissions', m.permissions.map((p) => PERMISSION_TEXT[p] || p).join(' · ') || '—']);
    rows.push(['Contenu', addon.script !== null && addon.script !== undefined ? 'manifest.json + script.js' : 'manifest.json']);
  }
  if (addon.errors.length > 0) { show('Cet add-on ne peut pas être posté', rows, addon.errors); return; }
  show('Add-on valide', rows, []);
  current = file;
  $('send').disabled = !session.authenticated;
  if (!session.authenticated) {
    $('status').textContent = 'Connecte-toi ci-dessus pour envoyer cet add-on.';
  }
}

$('file').addEventListener('change', (e) => choose(e.target.files[0]));
const drop = $('drop');
['dragenter', 'dragover'].forEach((t) => drop.addEventListener(t, (e) => { e.preventDefault(); drop.classList.add('over'); }));
['dragleave', 'drop'].forEach((t) => drop.addEventListener(t, (e) => { e.preventDefault(); drop.classList.remove('over'); }));
drop.addEventListener('drop', (e) => choose(e.dataTransfer.files[0]));
$('reset').addEventListener('click', () => { $('file').value = ''; $('file').click(); });

$('send').addEventListener('click', async () => {
  if (!current) return;
  $('send').disabled = true;
  $('status').className = 'ok';
  $('status').textContent = 'Envoi en cours…';
  const form = new FormData();
  form.append('zip', current, current.name);
  form.append('site_web', $('site_web').value);
  try {
    const response = await fetch('api/proposer', { method: 'POST', body: form });
    const data = await response.json().catch(() => ({}));
    if (!response.ok) {
      if (response.status === 401) {
        session = { authenticated: false, user: null };
        updateAuthBanner();
      }
      $('status').textContent = data.error || 'L’envoi a échoué.';
      if (data.details) show('Cet add-on ne peut pas être posté', [], data.details);
      $('send').disabled = !session.authenticated;
      return;
    }
    $('status').className = 'ok success';
    $('status').textContent = '';
    $('status').append(`Merci ${session.user?.username || ''} ! Ta proposition est envoyée. Le mainteneur va la relire. `,
      el('a', { href: data.url, target: '_blank', rel: 'noopener', text: 'Voir la proposition' }));
    current = null;
  } catch {
    $('status').textContent = 'Impossible de joindre le site. Vérifie ta connexion et réessaie.';
    $('send').disabled = !session.authenticated;
  }
});

session = await initAuthNav();
updateAuthBanner();

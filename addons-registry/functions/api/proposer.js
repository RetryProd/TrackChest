// POST /api/proposer : reçoit un add-on (.zip) envoyé depuis le site par un créateur connecté, le vérifie et ouvre une
// proposition (pull request) sur le dépôt GitHub du catalogue. Le mainteneur la relit avant de l'accepter.
//
// Configuration (Cloudflare Pages → projet trackchest-addons) :
//   GITHUB_TOKEN  (secret)  jeton GitHub limité au dépôt du catalogue : Contents et Pull requests en écriture.
//   GITHUB_REPO   (option)  « propriétaire/dépôt », par défaut RetryProd/trackchest-addons.
import { readAddonZip, LIMITS } from '../../site-src/addon-rules.js';
import { getUserSession, findUserByLogin } from '../_lib/users.js';
import { isRateLimited, recordHit, sameOrigin } from '../_lib/auth.js';
import { decodeText } from '../_lib/github.js';
import { parseRegistry } from '../_lib/catalog.js';

const DEFAULT_REPO = 'RetryProd/trackchest-addons';

function reply(status, body) {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'content-type': 'application/json; charset=utf-8', 'cache-control': 'no-store' },
  });
}

/**
 * Texte d'un add-on (nom, description) inséré dans une proposition GitHub : neutralise le Markdown, les liens, les
 * mentions (@quelqu'un), les références (#123), le HTML (dont le marqueur tc-author) et les retours à la ligne.
 */
function md(value) {
  let out = '';
  for (const ch of String(value ?? '').slice(0, 400)) {
    const code = ch.codePointAt(0);
    if (code < 0x20 || (code >= 0x7f && code <= 0x9f) || code === 0x2028 || code === 0x2029) out += ' ';
    else if (ch === '@') out += '@' + ZERO_WIDTH_SPACE; // plus de mention qui notifie quelqu'un
    else if (MARKDOWN_SPECIAL.has(ch)) out += BACKSLASH + ch;
    else out += ch;
  }
  return out;
}

const ZERO_WIDTH_SPACE = String.fromCharCode(0x200b);
const BACKSLASH = String.fromCharCode(92);
const MARKDOWN_SPECIAL = new Set([BACKSLASH, ...'`*_{}[]()<>#+!|~=-']);

function toBase64(bytes) {
  let binary = '';
  for (let i = 0; i < bytes.length; i += 0x8000) binary += String.fromCharCode.apply(null, bytes.subarray(i, i + 0x8000));
  return btoa(binary);
}

async function github(env, method, path, body) {
  const response = await fetch(`https://api.github.com${path}`, {
    method,
    headers: {
      authorization: `Bearer ${env.GITHUB_TOKEN}`,
      accept: 'application/vnd.github+json',
      'x-github-api-version': '2022-11-28',
      'user-agent': 'trackchest-addons-site',
      ...(body ? { 'content-type': 'application/json' } : {}),
    },
    body: body ? JSON.stringify(body) : undefined,
  });
  const data = response.status === 204 ? null : await response.json().catch(() => null);
  return { ok: response.ok, status: response.status, data };
}

export async function onRequestPost({ request, env }) {
  if (!env.GITHUB_TOKEN) return reply(503, { error: 'L’envoi d’add-ons n’est pas encore configuré sur ce site.' });

  // Requêtes venues d'une page du site seulement (protection simple contre les envois automatisés d'autres sites).
  if (!sameOrigin(request)) return reply(403, { error: 'Envoi refusé.' });

  const session = await getUserSession(request, env);
  if (!session) return reply(401, { error: 'Connecte-toi avec ton compte TrackChest pour poster un add-on.' });
  // Le compte doit toujours exister (un compte supprimé garde un cookie signé valide jusqu'à son expiration).
  let account = null;
  try { account = await findUserByLogin(env, session.usernameLower); }
  catch { return reply(502, { error: 'Impossible de vérifier ton compte pour le moment.' }); }
  if (!account || account.usernameLower !== session.usernameLower) return reply(401, { error: 'Ton compte est introuvable : reconnecte-toi.' });
  // 10 propositions par compte et par heure : évite d'inonder le dépôt GitHub.
  if (await isRateLimited(request, env, 'propose', session.usernameLower)) {
    return reply(429, { error: 'Trop de propositions envoyées depuis une heure. Réessaie plus tard.' });
  }

  const length = Number(request.headers.get('content-length') || 0);
  if (length > LIMITS.zip * 2) return reply(413, { error: 'Fichier trop gros (64 Ko maximum).' });

  let form;
  try { form = await request.formData(); } catch { return reply(400, { error: 'Envoi illisible.' }); }
  if (form.get('site_web')) return reply(400, { error: 'Envoi refusé.' }); // champ piège invisible pour les robots
  const file = form.get('zip');
  if (!file || typeof file === 'string') return reply(400, { error: 'Choisis un fichier .zip.' });
  if (file.size > LIMITS.zip) return reply(413, { error: 'Fichier trop gros (64 Ko maximum).' });

  const addon = await readAddonZip(await file.arrayBuffer());
  if (addon.errors.length > 0) return reply(400, { error: 'Add-on invalide.', details: addon.errors });
  const { manifest, manifestText, script } = addon;

  const repo = env.GITHUB_REPO || DEFAULT_REPO;
  const base = await github(env, 'GET', `/repos/${repo}/git/ref/heads/main`);
  if (!base.ok) {
    return [401, 403, 404].includes(base.status)
      ? reply(503, { error: 'L’envoi est mal configuré sur ce site (jeton GitHub invalide ou sans accès au dépôt).' })
      : reply(502, { error: 'GitHub ne répond pas, réessaie plus tard.' });
  }
  const existing = await github(env, 'GET', `/repos/${repo}/contents/addons/${manifest.id}/manifest.json?ref=main`);
  const update = existing.ok;
  if (update) {
    // Vérification de propriétaire « fermée par défaut » : si registry.json est illisible, on refuse la mise à jour.
    const regFile = await github(env, 'GET', `/repos/${repo}/contents/registry.json?ref=main`);
    if (!regFile.ok && regFile.status !== 404) return reply(502, { error: 'GitHub ne répond pas, réessaie plus tard.' });
    const reg = regFile.ok && regFile.data?.content ? parseRegistry(decodeText(regFile.data.content)) : { authors: {} };
    const owner = reg.authors?.[manifest.id];
    if (owner && owner.toLowerCase() !== session.usernameLower) {
      return reply(403, { error: `L’add-on « ${manifest.id} » appartient au créateur ${owner} : seul son auteur peut le mettre à jour.` });
    }
  }

  await recordHit(request, env, 'propose', session.usernameLower);
  const branch = `proposition/${manifest.id}-${manifest.version}-${Date.now().toString(36)}`;
  const created = await github(env, 'POST', `/repos/${repo}/git/refs`, { ref: `refs/heads/${branch}`, sha: base.data.object.sha });
  if (!created.ok) return reply(502, { error: 'Impossible de préparer la proposition sur GitHub.' });

  const encoder = new TextEncoder();
  const files = [['manifest.json', manifestText]];
  if (script !== null) files.push(['script.js', script]);
  for (const [name, content] of files) {
    const path = `addons/${manifest.id}/${name}`;
    const current = update ? await github(env, 'GET', `/repos/${repo}/contents/${path}?ref=${encodeURIComponent(branch)}`) : { ok: false };
    const put = await github(env, 'PUT', `/repos/${repo}/contents/${path}`, {
      message: `${update ? 'Mise à jour' : 'Ajout'} de l'add-on ${manifest.id} ${manifest.version}`,
      content: toBase64(encoder.encode(content)),
      branch,
      ...(current.ok && current.data?.sha ? { sha: current.data.sha } : {}),
    });
    if (!put.ok) return reply(502, { error: 'Impossible d’envoyer les fichiers sur GitHub.' });
  }

  const permissions = manifest.permissions.map((p) => `\`${p}\``).join(', ');
  const body = [
    `Proposition envoyée depuis le site par **${session.username}**.`,
    `<!-- tc-author: ${session.username} -->`,
    '',
    `- **Auteur** : \`${session.username}\` (compte TrackChest)`,
    `- **Add-on** : ${md(manifest.name)} (\`${manifest.id}\`) ${manifest.version}${update ? ' — mise à jour d’un add-on existant' : ''}`,
    `- **Permissions** : ${permissions}`,
    `- **Description** : ${md(manifest.description) || '—'}`,
    script !== null ? '- **Contient un script** (`script.js`) : à lire entièrement.' : '',
    '',
    'Avant d’accepter (voir la checklist du README) :',
    '- [ ] Les contrôles automatiques passent.',
    '- [ ] Le nom, la description et les libellés sont honnêtes.',
    script !== null ? '- [ ] Le script est lu en entier et testé : `.\\scripts\\addon.ps1 empaqueter ' + manifest.id + ' -Exe …\\TrackChest.exe`' : '',
    '- [ ] Validation depuis la page Administration (`/admin.html`).',
  ].filter((line) => line !== '').join('\n');

  const pull = await github(env, 'POST', `/repos/${repo}/pulls`, {
    title: `${update ? 'Mise à jour' : 'Nouvel add-on'} : ${String(manifest.name).replace(/@/g, '@' + ZERO_WIDTH_SPACE)} ${manifest.version} (par ${session.username})`,
    head: branch,
    base: 'main',
    body,
  });
  if (!pull.ok) return reply(502, { error: 'Impossible de créer la proposition sur GitHub.' });
  return reply(200, { ok: true, url: pull.data.html_url, number: pull.data.number, update, author: session.username });
}

export function onRequestGet() {
  return reply(405, { error: 'Utilise le formulaire du site pour poster un add-on.' });
}

// Connexion de l'administrateur : un mot de passe (secret ADMIN_PASSWORD) et un cookie de session signé.
//
// - Le mot de passe est comparé en temps constant ; chaque échec attend 1,2 s et, après 5 échecs en 10 minutes
//   depuis la même adresse, les tentatives suivantes sont refusées.
// - Les compteurs d'essais sont gardés dans Cloudflare KV (USERS_KV) quand il est lié : ils valent alors pour toutes
//   les instances du site, et pas seulement pour celle qui a reçu la requête (repli en mémoire sinon).
// - Le cookie contient une date d'expiration et sa signature HMAC. Il est HttpOnly, Secure et SameSite=Strict :
//   ni un script de page, ni un autre site ne peut s'en servir. Changer le mot de passe invalide toutes les sessions.
// - Les actions qui modifient quelque chose exigent en plus que la requête vienne d'une page du site.

const encoder = new TextEncoder();
const COOKIE = 'tc_admin';
const LIFETIME_MS = 8 * 60 * 60 * 1000;

// Limites d'essais : nombre maximal d'événements par fenêtre, par adresse IP (ou par compte pour « account »).
const RATE_LIMITS = {
  admin: { max: 5, windowMs: 10 * 60 * 1000 },      // échecs de connexion à l'administration
  login: { max: 8, windowMs: 10 * 60 * 1000 },      // échecs de connexion utilisateur, par adresse
  account: { max: 10, windowMs: 15 * 60 * 1000 },   // échecs de connexion sur un même compte (toutes adresses)
  register: { max: 3, windowMs: 60 * 60 * 1000 },   // comptes créés par adresse
  propose: { max: 10, windowMs: 60 * 60 * 1000 },   // propositions envoyées par compte
};

const memoryHits = new Map(); // clé → [dates] (repli si KV n'est pas lié)

/** Adresse du client ; une adresse IPv6 est regroupée par /64 (un abonné reçoit souvent tout un bloc). */
export function clientAddress(request) {
  const ip = request.headers.get('cf-connecting-ip') || 'inconnue';
  return ip.includes(':') ? ip.split(':').slice(0, 4).join(':') + '::/64' : ip;
}

function limitKey(scope, subject) {
  return `rl:${scope}:${String(subject).toLowerCase().slice(0, 120)}`;
}

async function readHits(env, scope, subject) {
  const { windowMs } = RATE_LIMITS[scope];
  const key = limitKey(scope, subject);
  let hits = memoryHits.get(key) || [];
  if (env?.USERS_KV && typeof env.USERS_KV.get === 'function') {
    try {
      const stored = JSON.parse((await env.USERS_KV.get(key)) || '[]');
      if (Array.isArray(stored)) hits = [...new Set([...hits, ...stored])];
    } catch { /* compteur illisible : ignoré */ }
  }
  const now = Date.now();
  return hits.filter((t) => typeof t === 'number' && now - t < windowMs);
}

/** Vrai quand la limite « scope » est atteinte pour ce sujet (par défaut : l'adresse du client). */
export async function isRateLimited(request, env, scope, subject = clientAddress(request)) {
  return (await readHits(env, scope, subject)).length >= RATE_LIMITS[scope].max;
}

/** Compte un événement (un échec, une inscription, un envoi…). */
export async function recordHit(request, env, scope, subject = clientAddress(request)) {
  const { windowMs } = RATE_LIMITS[scope];
  const key = limitKey(scope, subject);
  const hits = [...(await readHits(env, scope, subject)), Date.now()].slice(-50);
  memoryHits.set(key, hits);
  if (env?.USERS_KV && typeof env.USERS_KV.put === 'function') {
    try { await env.USERS_KV.put(key, JSON.stringify(hits), { expirationTtl: Math.max(60, Math.ceil(windowMs / 1000)) }); }
    catch { /* KV indisponible : le repli en mémoire reste actif */ }
  }
}

/** Remet le compteur à zéro (après une connexion réussie). */
export async function clearHits(request, env, scope, subject = clientAddress(request)) {
  const key = limitKey(scope, subject);
  memoryHits.delete(key);
  if (env?.USERS_KV && typeof env.USERS_KV.delete === 'function') {
    try { await env.USERS_KV.delete(key); } catch { /* sans importance */ }
  }
}

/** Vrai quand la requête vient d'une page du site (en-tête Origin obligatoire et identique). */
export function sameOrigin(request) {
  const origin = request.headers.get('origin');
  return Boolean(origin) && origin === new URL(request.url).origin;
}

const hex = (bytes) => [...new Uint8Array(bytes)].map((b) => b.toString(16).padStart(2, '0')).join('');

async function hmacHex(secret, message) {
  const key = await crypto.subtle.importKey('raw', encoder.encode(secret), { name: 'HMAC', hash: 'SHA-256' }, false, ['sign']);
  return hex(await crypto.subtle.sign('HMAC', key, encoder.encode(message)));
}

async function digest(text) {
  return new Uint8Array(await crypto.subtle.digest('SHA-256', encoder.encode(text)));
}

function sameBytes(a, b) {
  if (a.length !== b.length) return false;
  let diff = 0;
  for (let i = 0; i < a.length; i++) diff |= a[i] ^ b[i];
  return diff === 0;
}

export const sleep = (ms) => new Promise((resolve) => setTimeout(resolve, ms));

export async function passwordMatches(env, given) {
  if (typeof given !== 'string' || given.length === 0 || given.length > 200) return false;
  return sameBytes(await digest(given), await digest(env.ADMIN_PASSWORD));
}

export async function sessionCookie(request, env) {
  const expires = Date.now() + LIFETIME_MS;
  const signature = await hmacHex(`session|${env.ADMIN_PASSWORD}`, String(expires));
  const secure = new URL(request.url).protocol === 'https:' ? 'Secure; ' : '';
  return `${COOKIE}=${expires}.${signature}; HttpOnly; ${secure}SameSite=Strict; Path=/; Max-Age=${LIFETIME_MS / 1000}`;
}

export function endedCookie() {
  return `${COOKIE}=; HttpOnly; Secure; SameSite=Strict; Path=/; Max-Age=0`;
}

async function hasSession(request, env) {
  const header = request.headers.get('cookie') || '';
  const part = header.split(';').map((s) => s.trim()).find((s) => s.startsWith(`${COOKIE}=`));
  if (!part) return false;
  const [expires, signature] = part.slice(COOKIE.length + 1).split('.');
  if (!/^\d{10,15}$/.test(expires || '') || !/^[0-9a-f]{64}$/.test(signature || '')) return false;
  if (Number(expires) < Date.now()) return false;
  const expected = await hmacHex(`session|${env.ADMIN_PASSWORD}`, expires);
  return sameBytes(encoder.encode(expected), encoder.encode(signature));
}

export function reply(status, body, headers = {}) {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'content-type': 'application/json; charset=utf-8', 'cache-control': 'no-store', ...headers },
  });
}

/** À appeler au début de chaque route admin. Renvoie une réponse d'erreur, ou null si l'accès est autorisé. */
export async function guard(request, env) {
  if (!env.ADMIN_PASSWORD) return reply(503, { error: 'L’administration n’est pas encore configurée (ADMIN_PASSWORD).' });
  if (request.method !== 'GET' && !sameOrigin(request)) return reply(403, { error: 'Requête refusée.' });
  if (!(await hasSession(request, env))) return reply(401, { error: 'Connecte-toi.' });
  if (!env.GITHUB_TOKEN) return reply(503, { error: 'Le jeton GitHub n’est pas configuré (GITHUB_TOKEN).' });
  return null;
}

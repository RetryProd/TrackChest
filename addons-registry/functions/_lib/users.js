// Comptes créateurs / utilisateurs du site TrackChest Add-ons.
//
// Stockage pris en charge (par ordre de priorité) :
//   1. Cloudflare D1  (binding env.DB)        — table `users` créée automatiquement
//   2. Cloudflare KV  (binding env.USERS_KV)  — clés `user:<pseudo_min>` et `email:<email_min>`
//   3. Repli chiffré  (env.GITHUB_TOKEN)      — blob chiffré en AES-GCM-256 dans `refs/meta/users` sur GitHub
//   4. Mémoire locale                         — pour les essais hors-ligne en développement
//
// Sécurité :
//   - Mots de passe hachés avec PBKDF2-HMAC-SHA256 (100 000 itérations, sel aléatoire de 16 octets), comparés en temps constant.
//   - Session dans un cookie signé HMAC-SHA256 (`tc_user`), HttpOnly, Secure (en HTTPS), SameSite=Lax, valable 30 jours.
//   - Clé de session : le secret USER_SESSION_SECRET (32 caractères ou plus, à créer). À défaut, ADMIN_PASSWORD est
//     utilisé pour rester compatible ; aucune clé écrite dans le code n'est utilisée en ligne.

import { gh, repoOf, toBase64, fromBase64 } from './github.js';

const encoder = new TextEncoder();
const decoder = new TextDecoder();

export const USER_COOKIE = 'tc_user';
const USER_LIFETIME_MS = 30 * 24 * 60 * 60 * 1000;
const PBKDF2_ITERATIONS = 100_000;
const RESERVED_NAMES = new Set([
  'admin', 'administrateur', 'administrator', 'root', 'system',
  'trackchest', 'retryprod', 'support', 'mod', 'moderateur', 'null', 'undefined',
]);

const memoryUsers = new Map(); // pseudo_min → user
let d1Initialized = false;

const hex = (bytes) => [...new Uint8Array(bytes)].map((b) => b.toString(16).padStart(2, '0')).join('');

function fromHex(hexString) {
  const clean = String(hexString || '');
  if (clean.length % 2 !== 0 || !/^[0-9a-fA-F]*$/.test(clean)) return new Uint8Array(0);
  const out = new Uint8Array(clean.length / 2);
  for (let i = 0; i < out.length; i++) out[i] = parseInt(clean.slice(i * 2, i * 2 + 2), 16);
  return out;
}

function sameBytes(a, b) {
  if (a.length !== b.length) return false;
  let diff = 0;
  for (let i = 0; i < a.length; i++) diff |= a[i] ^ b[i];
  return diff === 0;
}

function toBase64Url(text) {
  return toBase64(encoder.encode(text)).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
}

function fromBase64Url(b64url) {
  const padded = String(b64url).replace(/-/g, '+').replace(/_/g, '/');
  return decoder.decode(fromBase64(padded));
}

function sessionSecret(env) {
  if (env.USER_SESSION_SECRET) {
    if (env.USER_SESSION_SECRET.length < 32) throw new Error('USER_SESSION_SECRET est trop court (32 caractères minimum).');
    return env.USER_SESSION_SECRET;
  }
  if (env.ADMIN_PASSWORD) return env.ADMIN_PASSWORD; // compatibilité : à remplacer par USER_SESSION_SECRET
  // Clé fixe uniquement pour les essais hors ligne (comptes en mémoire, aucun stockage configuré).
  if (!env.DB && !env.USERS_KV && !env.GITHUB_TOKEN) return 'trackchest-dev-local-secret';
  throw new Error('Le secret USER_SESSION_SECRET n’est pas configuré.');
}

async function hmacHex(secret, message) {
  const key = await crypto.subtle.importKey('raw', encoder.encode(secret), { name: 'HMAC', hash: 'SHA-256' }, false, ['sign']);
  return hex(await crypto.subtle.sign('HMAC', key, encoder.encode(message)));
}

// ---- Hachage de mot de passe (PBKDF2-HMAC-SHA256) ---------------------------------------------------------------

export async function hashPassword(password, saltHex = null) {
  const salt = saltHex ? fromHex(saltHex) : crypto.getRandomValues(new Uint8Array(16));
  const key = await crypto.subtle.importKey('raw', encoder.encode(password), 'PBKDF2', false, ['deriveBits']);
  const bits = await crypto.subtle.deriveBits(
    { name: 'PBKDF2', hash: 'SHA-256', salt, iterations: PBKDF2_ITERATIONS },
    key,
    256,
  );
  return { salt: hex(salt), hash: hex(bits) };
}

export async function verifyPassword(password, saltHex, expectedHashHex) {
  if (typeof password !== 'string' || !saltHex || !expectedHashHex) return false;
  const { hash } = await hashPassword(password, saltHex);
  return sameBytes(encoder.encode(hash), encoder.encode(expectedHashHex));
}

// ---- Validation des champs --------------------------------------------------------------------------------------

export function validateUsername(username) {
  const clean = typeof username === 'string' ? username.trim() : '';
  if (clean.length < 3 || clean.length > 24) return 'Le pseudo doit contenir entre 3 et 24 caractères.';
  if (!/^[A-Za-z0-9_-]+$/.test(clean)) return 'Le pseudo ne peut contenir que des lettres, chiffres, tirets (-) et tirets bas (_).';
  if (RESERVED_NAMES.has(clean.toLowerCase())) return 'Ce pseudo est réservé.';
  return null;
}

export function validateEmail(email) {
  const clean = typeof email === 'string' ? email.trim() : '';
  if (!clean) return null; // facultatif
  if (clean.length > 120 || !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(clean)) return 'Adresse e-mail invalide.';
  return null;
}

const COMMON_PASSWORDS = new Set([
  '12345678', '123456789', '1234567890', 'password', 'password1', 'motdepasse', 'azertyuiop', 'qwertyuiop',
  'azerty123', 'qwerty123', '11111111', '00000000', 'iloveyou', 'fortnite', 'fortnite123', 'trackchest',
]);

export function validatePassword(password, username = '') {
  if (typeof password !== 'string' || password.length < 8) return 'Le mot de passe doit contenir au moins 8 caractères.';
  if (password.length > 200) return 'Le mot de passe est trop long (200 caractères maximum).';
  const lower = password.toLowerCase();
  if (COMMON_PASSWORDS.has(lower) || new Set(password).size === 1) return 'Ce mot de passe est trop courant : choisis-en un autre.';
  if (username && lower.includes(String(username).toLowerCase())) return 'Le mot de passe ne doit pas contenir ton pseudo.';
  return null;
}

// ---- Stockage (D1, KV, ou repli chiffré GitHub / mémoire) -------------------------------------------------------

async function ensureD1(db) {
  if (d1Initialized) return;
  await db.prepare(
    `CREATE TABLE IF NOT EXISTS users (
      username_lower TEXT PRIMARY KEY,
      username TEXT NOT NULL,
      email TEXT,
      email_lower TEXT,
      salt TEXT NOT NULL,
      hash TEXT NOT NULL,
      created_at TEXT NOT NULL
    )`
  ).run();
  await db.prepare(`CREATE INDEX IF NOT EXISTS idx_users_email ON users(email_lower)`).run();
  d1Initialized = true;
}

async function encryptionKey(env) {
  const digest = await crypto.subtle.digest('SHA-256', encoder.encode(`users-aes|${sessionSecret(env)}`));
  return crypto.subtle.importKey('raw', digest, { name: 'AES-GCM' }, false, ['encrypt', 'decrypt']);
}

async function encryptJson(env, data) {
  const iv = crypto.getRandomValues(new Uint8Array(12));
  const key = await encryptionKey(env);
  const cipher = await crypto.subtle.encrypt({ name: 'AES-GCM', iv }, key, encoder.encode(JSON.stringify(data)));
  return `${hex(iv)}.${toBase64(new Uint8Array(cipher))}`;
}

async function decryptJson(env, payload) {
  const [ivHex, b64] = String(payload || '').split('.');
  if (!ivHex || !b64) return { users: {} };
  const key = await encryptionKey(env);
  const plain = await crypto.subtle.decrypt({ name: 'AES-GCM', iv: fromHex(ivHex) }, key, fromBase64(b64));
  return JSON.parse(decoder.decode(plain));
}

const USERS_REF = 'meta/users';

async function loadGithubStore(env) {
  const repo = repoOf(env);
  const ref = await gh(env, 'GET', `/repos/${repo}/git/ref/${USERS_REF}`);
  if (!ref.ok || !ref.data?.object?.sha) return { sha: null, users: {} };
  const blob = await gh(env, 'GET', `/repos/${repo}/git/blobs/${ref.data.object.sha}`);
  if (!blob.ok || !blob.data?.content) throw new Error('Stockage des comptes illisible pour le moment.');
  try {
    const raw = decoder.decode(fromBase64(blob.data.content));
    const parsed = await decryptJson(env, raw.trim());
    return { sha: ref.data.object.sha, users: parsed.users || {} };
  } catch {
    // Clé changée ou données abîmées : on refuse plutôt que de repartir d'un stockage vide (qui écraserait les comptes).
    throw new Error('Stockage des comptes illisible (le secret de session a-t-il changé ?).');
  }
}

async function saveGithubStore(env, users, previousSha) {
  const repo = repoOf(env);
  const encrypted = await encryptJson(env, { version: 1, users });
  const blob = await gh(env, 'POST', `/repos/${repo}/git/blobs`, { content: encrypted, encoding: 'utf-8' });
  if (!blob.ok) throw new Error('Impossible d’enregistrer le compte.');
  const newSha = blob.data.sha;
  if (previousSha) {
    const updated = await gh(env, 'PATCH', `/repos/${repo}/git/refs/${USERS_REF}`, { sha: newSha, force: true });
    if (!updated.ok) throw new Error('Impossible de mettre à jour les comptes.');
  } else {
    const created = await gh(env, 'POST', `/repos/${repo}/git/refs`, { ref: `refs/${USERS_REF}`, sha: newSha });
    if (!created.ok) throw new Error('Impossible d’initialiser le stockage des comptes.');
  }
}

export async function findUserByLogin(env, login) {
  const key = String(login || '').trim().toLowerCase();
  if (!key) return null;

  // 1. Cloudflare D1
  if (env.DB && typeof env.DB.prepare === 'function') {
    await ensureD1(env.DB);
    const row = await env.DB
      .prepare('SELECT username, username_lower, email, email_lower, salt, hash, created_at FROM users WHERE username_lower = ?1 OR (email_lower IS NOT NULL AND email_lower = ?1) LIMIT 1')
      .bind(key)
      .first();
    if (!row) return null;
    return {
      username: row.username,
      usernameLower: row.username_lower,
      email: row.email || '',
      emailLower: row.email_lower || '',
      salt: row.salt,
      hash: row.hash,
      createdAt: row.created_at,
    };
  }

  // 2. Cloudflare KV
  if (env.USERS_KV && typeof env.USERS_KV.get === 'function') {
    let usernameLower = key;
    if (key.includes('@')) {
      const mapped = await env.USERS_KV.get(`email:${key}`);
      if (!mapped) return null;
      usernameLower = mapped;
    }
    const raw = await env.USERS_KV.get(`user:${usernameLower}`);
    if (!raw) return null;
    try { return JSON.parse(raw); } catch { return null; }
  }

  // 3. Repli chiffré sur GitHub (si GITHUB_TOKEN est configuré)
  if (env.GITHUB_TOKEN) {
    const { users } = await loadGithubStore(env);
    if (users[key]) return users[key];
    if (key.includes('@')) {
      return Object.values(users).find((u) => u.emailLower && u.emailLower === key) || null;
    }
    return null;
  }

  // 4. Mémoire locale
  if (memoryUsers.has(key)) return memoryUsers.get(key);
  if (key.includes('@')) {
    for (const u of memoryUsers.values()) if (u.emailLower && u.emailLower === key) return u;
  }
  return null;
}

export async function registerUser(env, { username, email, password }) {
  const cleanUsername = String(username || '').trim();
  const cleanEmail = String(email || '').trim();
  const userErr = validateUsername(cleanUsername);
  if (userErr) return { ok: false, status: 400, error: userErr };
  const emailErr = validateEmail(cleanEmail);
  if (emailErr) return { ok: false, status: 400, error: emailErr };
  const passErr = validatePassword(password, cleanUsername);
  if (passErr) return { ok: false, status: 400, error: passErr };

  const usernameLower = cleanUsername.toLowerCase();
  const emailLower = cleanEmail ? cleanEmail.toLowerCase() : '';
  const { salt, hash } = await hashPassword(password);
  const record = {
    username: cleanUsername,
    usernameLower,
    email: cleanEmail,
    emailLower,
    salt,
    hash,
    createdAt: new Date().toISOString(),
  };

  // 1. Cloudflare D1
  if (env.DB && typeof env.DB.prepare === 'function') {
    await ensureD1(env.DB);
    const existingUser = await env.DB.prepare('SELECT 1 FROM users WHERE username_lower = ?1').bind(usernameLower).first();
    if (existingUser) return { ok: false, status: 409, error: 'Ce pseudo est déjà pris.' };
    if (emailLower) {
      const existingEmail = await env.DB.prepare('SELECT 1 FROM users WHERE email_lower = ?1').bind(emailLower).first();
      if (existingEmail) return { ok: false, status: 409, error: 'Cette adresse e-mail est déjà associée à un compte.' };
    }
    await env.DB
      .prepare('INSERT INTO users (username_lower, username, email, email_lower, salt, hash, created_at) VALUES (?1, ?2, ?3, ?4, ?5, ?6, ?7)')
      .bind(usernameLower, cleanUsername, cleanEmail || null, emailLower || null, salt, hash, record.createdAt)
      .run();
    return { ok: true, user: publicUser(record) };
  }

  // 2. Cloudflare KV
  if (env.USERS_KV && typeof env.USERS_KV.get === 'function') {
    const existingUser = await env.USERS_KV.get(`user:${usernameLower}`);
    if (existingUser) return { ok: false, status: 409, error: 'Ce pseudo est déjà pris.' };
    if (emailLower) {
      const existingEmail = await env.USERS_KV.get(`email:${emailLower}`);
      if (existingEmail) return { ok: false, status: 409, error: 'Cette adresse e-mail est déjà associée à un compte.' };
    }
    await env.USERS_KV.put(`user:${usernameLower}`, JSON.stringify(record));
    if (emailLower) await env.USERS_KV.put(`email:${emailLower}`, usernameLower);
    return { ok: true, user: publicUser(record) };
  }

  // 3. Repli chiffré GitHub
  if (env.GITHUB_TOKEN) {
    const { sha, users } = await loadGithubStore(env);
    if (users[usernameLower]) return { ok: false, status: 409, error: 'Ce pseudo est déjà pris.' };
    if (emailLower && Object.values(users).some((u) => u.emailLower === emailLower)) {
      return { ok: false, status: 409, error: 'Cette adresse e-mail est déjà associée à un compte.' };
    }
    users[usernameLower] = record;
    await saveGithubStore(env, users, sha);
    return { ok: true, user: publicUser(record) };
  }

  // 4. Mémoire locale
  if (memoryUsers.has(usernameLower)) return { ok: false, status: 409, error: 'Ce pseudo est déjà pris.' };
  if (emailLower) {
    for (const u of memoryUsers.values()) {
      if (u.emailLower === emailLower) return { ok: false, status: 409, error: 'Cette adresse e-mail est déjà associée à un compte.' };
    }
  }
  memoryUsers.set(usernameLower, record);
  return { ok: true, user: publicUser(record) };
}

export async function authenticateUser(env, login, password) {
  if (typeof login !== 'string' || typeof password !== 'string' || !login.trim() || !password) return null;
  const user = await findUserByLogin(env, login);
  if (!user) {
    // Calcul à blanc pour éviter de révéler si le compte existe par le temps de réponse.
    await hashPassword(password, '000102030405060708090a0b0c0d0e0f');
    return null;
  }
  const valid = await verifyPassword(password, user.salt, user.hash);
  return valid ? publicUser(user) : null;
}

export function publicUser(u) {
  return {
    username: u.username,
    usernameLower: u.usernameLower,
    email: u.email || '',
    createdAt: u.createdAt,
  };
}

// ---- Cookie de session utilisateur ------------------------------------------------------------------------------

export async function userSessionCookie(request, env, user) {
  const payloadObj = {
    u: user.username,
    sub: user.usernameLower || user.username.toLowerCase(),
    exp: Date.now() + USER_LIFETIME_MS,
  };
  const payload = toBase64Url(JSON.stringify(payloadObj));
  const signature = await hmacHex(`user-session|${sessionSecret(env)}`, payload);
  const secure = new URL(request.url).protocol === 'https:' ? 'Secure; ' : '';
  return `${USER_COOKIE}=${payload}.${signature}; HttpOnly; ${secure}SameSite=Lax; Path=/; Max-Age=${USER_LIFETIME_MS / 1000}`;
}

export function endedUserCookie(request) {
  const secure = request && new URL(request.url).protocol === 'https:' ? 'Secure; ' : '';
  return `${USER_COOKIE}=; HttpOnly; ${secure}SameSite=Lax; Path=/; Max-Age=0`;
}

export async function getUserSession(request, env) {
  try { return await readUserSession(request, env); } catch { return null; }
}

async function readUserSession(request, env) {
  const header = request.headers.get('cookie') || '';
  const part = header.split(';').map((s) => s.trim()).find((s) => s.startsWith(`${USER_COOKIE}=`));
  if (!part) return null;
  const value = part.slice(USER_COOKIE.length + 1);
  const dot = value.indexOf('.');
  if (dot <= 0) return null;
  const payload = value.slice(0, dot);
  const signature = value.slice(dot + 1);
  if (!/^[0-9a-f]{64}$/.test(signature)) return null;
  const expected = await hmacHex(`user-session|${sessionSecret(env)}`, payload);
  if (!sameBytes(encoder.encode(expected), encoder.encode(signature))) return null;
  try {
    const data = JSON.parse(fromBase64Url(payload));
    if (!data || typeof data.u !== 'string' || typeof data.sub !== 'string' || typeof data.exp !== 'number') return null;
    if (data.exp < Date.now() || data.exp > Date.now() + USER_LIFETIME_MS + 60_000) return null;
    if (!/^[a-z0-9_-]{3,24}$/.test(data.sub)) return null;
    return { username: data.u, usernameLower: data.sub };
  } catch {
    return null;
  }
}

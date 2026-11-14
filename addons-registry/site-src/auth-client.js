// Client d'authentification partagé par les pages du site TrackChest Add-ons.

let cachedSession = null;

export async function fetchSession({ force = false } = {}) {
  if (cachedSession && !force) return cachedSession;
  try {
    const response = await fetch('api/auth/me', { cache: 'no-store' });
    if (!response.ok) {
      cachedSession = { authenticated: false, user: null };
      return cachedSession;
    }
    const data = await response.json();
    cachedSession = {
      authenticated: Boolean(data?.authenticated && data?.user?.username),
      user: data?.user || null,
    };
  } catch {
    cachedSession = { authenticated: false, user: null };
  }
  return cachedSession;
}

export async function initAuthNav() {
  const navAuth = document.getElementById('navAuth');
  const session = await fetchSession();
  if (navAuth) {
    if (session.authenticated && session.user) {
      navAuth.textContent = `👤 ${session.user.username}`;
      navAuth.title = `Connecté en tant que ${session.user.username}`;
    } else {
      navAuth.textContent = 'Connexion';
      navAuth.title = 'Se connecter ou créer un compte';
    }
  }
  return session;
}

export async function loginUser(login, password) {
  const response = await fetch('api/auth/login', {
    method: 'POST',
    headers: { 'content-type': 'application/json' },
    body: JSON.stringify({ login, password }),
  });
  const data = await response.json().catch(() => ({}));
  if (!response.ok) throw new Error(data.error || 'Connexion impossible.');
  cachedSession = { authenticated: true, user: data.user };
  await initAuthNav();
  return data.user;
}

export async function registerAccount({ username, email, password, site_web = '' }) {
  const response = await fetch('api/auth/register', {
    method: 'POST',
    headers: { 'content-type': 'application/json' },
    body: JSON.stringify({ username, email, password, site_web }),
  });
  const data = await response.json().catch(() => ({}));
  if (!response.ok) throw new Error(data.error || 'Inscription impossible.');
  cachedSession = { authenticated: true, user: data.user };
  await initAuthNav();
  return data.user;
}

export async function logoutUser() {
  await fetch('api/auth/logout', { method: 'POST' }).catch(() => {});
  cachedSession = { authenticated: false, user: null };
  await initAuthNav();
}

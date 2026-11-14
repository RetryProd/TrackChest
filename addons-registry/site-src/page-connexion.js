// Script de la page connexion.html (séparé de la page : la politique de sécurité interdit les scripts en ligne).
import { initAuthNav, loginUser, registerAccount, logoutUser } from './auth-client.js';

const $ = (id) => document.getElementById(id);
const params = new URLSearchParams(location.search);
const retour = params.get('retour');

function safeRedirectUrl() {
  if (!retour) return null;
  if (/^[a-z0-9_-]+\.html$/i.test(retour)) return retour;
  return null;
}

function showTab(mode) {
  const isLogin = mode === 'login';
  $('loginBox').classList.toggle('hidden', !isLogin);
  $('registerBox').classList.toggle('hidden', isLogin);
  $('tabLogin').classList.toggle('active', isLogin);
  $('tabRegister').classList.toggle('active', !isLogin);
  $('loginError').textContent = '';
  $('registerError').textContent = '';
}

$('tabLogin').addEventListener('click', () => showTab('login'));
$('tabRegister').addEventListener('click', () => showTab('register'));

function el(tag, attrs = {}, children = []) {
  const e = document.createElement(tag);
  for (const [k, v] of Object.entries(attrs)) { if (k === 'text') e.textContent = v; else e.setAttribute(k, v); }
  children.forEach((c) => e.appendChild(c));
  return e;
}

async function loadMyAddons(username) {
  const box = $('myAddons');
  box.textContent = '';
  try {
    const response = await fetch('index.json', { cache: 'no-store' });
    const index = await response.json();
    const mine = (index.addons || []).filter((a) => a.author && a.author.toLowerCase() === username.toLowerCase());
    if (mine.length === 0) {
      box.appendChild(el('p', { class: 'empty', text: 'Tu n’as pas encore d’add-on publié dans le catalogue.' }));
      return;
    }
    mine.forEach((a) => {
      const meta = el('div', { class: 'meta' }, [
        el('span', { class: 'badge ' + (a.tier === 'verified' ? 'verified' : 'community'), text: a.tier === 'verified' ? '✓ Vérifié' : 'Communauté' }),
        el('span', { class: 'badge', text: 'v' + a.version }),
      ]);
      box.appendChild(el('article', { class: 'addon' }, [
        el('h3', { text: a.name }),
        meta,
        el('p', { text: a.description || 'Pas de description.' }),
      ]));
    });
  } catch {
    box.appendChild(el('p', { class: 'empty', text: 'Impossible de charger le catalogue.' }));
  }
}

function renderSession(session) {
  if (session.authenticated && session.user) {
    const target = safeRedirectUrl();
    if (target) {
      location.href = target;
      return;
    }
    $('guestView').classList.add('hidden');
    $('accountView').classList.remove('hidden');
    $('accUsername').textContent = session.user.username;
    if (session.user.email) {
      $('accEmailLabel').classList.remove('hidden');
      $('accEmail').classList.remove('hidden');
      $('accEmail').textContent = session.user.email;
    } else {
      $('accEmailLabel').classList.add('hidden');
      $('accEmail').classList.add('hidden');
    }
    loadMyAddons(session.user.username);
  } else {
    $('guestView').classList.remove('hidden');
    $('accountView').classList.add('hidden');
  }
}

$('loginForm').addEventListener('submit', async (e) => {
  e.preventDefault();
  $('loginError').textContent = '';
  $('btnLogin').disabled = true;
  try {
    const user = await loginUser($('loginId').value.trim(), $('loginPassword').value);
    $('loginPassword').value = '';
    renderSession({ authenticated: true, user });
  } catch (err) {
    $('loginError').textContent = err.message;
  }
  $('btnLogin').disabled = false;
});

$('registerForm').addEventListener('submit', async (e) => {
  e.preventDefault();
  $('registerError').textContent = '';
  const pass = $('regPassword').value;
  const confirm = $('regConfirm').value;
  if (pass !== confirm) {
    $('registerError').textContent = 'Les deux mots de passe ne correspondent pas.';
    return;
  }
  $('btnRegister').disabled = true;
  try {
    const user = await registerAccount({
      username: $('regUsername').value.trim(),
      email: $('regEmail').value.trim(),
      password: pass,
      site_web: $('site_web').value,
    });
    $('regPassword').value = '';
    $('regConfirm').value = '';
    renderSession({ authenticated: true, user });
  } catch (err) {
    $('registerError').textContent = err.message;
  }
  $('btnRegister').disabled = false;
});

$('btnLogout').addEventListener('click', async () => {
  await logoutUser();
  renderSession({ authenticated: false, user: null });
});

if (params.get('mode') === 'inscription') showTab('register');
const session = await initAuthNav();
renderSession(session);

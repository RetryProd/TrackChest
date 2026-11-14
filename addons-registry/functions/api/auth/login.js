import { isRateLimited, recordHit, clearHits, sameOrigin, sleep, reply } from '../../_lib/auth.js';
import { authenticateUser, userSessionCookie } from '../../_lib/users.js';

// POST /api/auth/login { login, password } : connecte un utilisateur par son pseudo ou son e-mail.
// Limites : 8 échecs par adresse en 10 minutes, et 10 échecs sur un même compte en 15 minutes (toutes adresses
// confondues, contre les essais répartis sur beaucoup d'adresses).
export async function onRequestPost({ request, env }) {
  if (!sameOrigin(request)) return reply(403, { error: 'Requête refusée.' });
  if (await isRateLimited(request, env, 'login')) return reply(429, { error: 'Trop d’essais. Réessaie dans quelques minutes.' });

  let body;
  try { body = await request.json(); } catch { return reply(400, { error: 'Requête illisible.' }); }
  const login = typeof body?.login === 'string' ? body.login.trim().toLowerCase().slice(0, 120) : '';
  if (login && (await isRateLimited(request, env, 'account', login))) {
    return reply(429, { error: 'Trop d’essais sur ce compte. Réessaie dans quelques minutes.' });
  }

  try {
    const user = await authenticateUser(env, body?.login, body?.password);
    if (!user) {
      await recordHit(request, env, 'login');
      if (login) await recordHit(request, env, 'account', login);
      await sleep(1200);
      return reply(401, { error: 'Pseudo / e-mail ou mot de passe incorrect.' });
    }
    await clearHits(request, env, 'login');
    return reply(
      200,
      { ok: true, user },
      { 'set-cookie': await userSessionCookie(request, env, user) },
    );
  } catch {
    return reply(502, { error: 'Impossible de vérifier le compte pour le moment.' });
  }
}

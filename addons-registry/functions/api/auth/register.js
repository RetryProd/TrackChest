import { isRateLimited, recordHit, sameOrigin, reply } from '../../_lib/auth.js';
import { registerUser, userSessionCookie } from '../../_lib/users.js';

// POST /api/auth/register { username, email, password, site_web } : crée un compte créateur et ouvre une session.
// Limite : 3 comptes créés par adresse et par heure (contre la création de comptes en masse).
export async function onRequestPost({ request, env }) {
  if (!sameOrigin(request)) return reply(403, { error: 'Requête refusée.' });
  if (await isRateLimited(request, env, 'register')) return reply(429, { error: 'Trop de comptes créés depuis cette adresse. Réessaie plus tard.' });

  let body;
  try { body = await request.json(); } catch { return reply(400, { error: 'Requête illisible.' }); }
  if (body?.site_web) return reply(400, { error: 'Inscription refusée.' }); // champ piège anti-robot

  try {
    const result = await registerUser(env, {
      username: body?.username,
      email: body?.email,
      password: body?.password,
    });
    if (!result.ok) return reply(result.status || 400, { error: result.error });
    await recordHit(request, env, 'register');
    return reply(
      200,
      { ok: true, user: result.user },
      { 'set-cookie': await userSessionCookie(request, env, result.user) },
    );
  } catch {
    return reply(502, { error: 'Impossible de créer le compte pour le moment.' });
  }
}

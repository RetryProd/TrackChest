import { reply, sameOrigin } from '../../_lib/auth.js';
import { endedUserCookie } from '../../_lib/users.js';

// POST /api/auth/logout : ferme la session utilisateur.
export async function onRequestPost({ request }) {
  if (!sameOrigin(request)) return reply(403, { error: 'Requête refusée.' });
  return reply(200, { ok: true }, { 'set-cookie': endedUserCookie(request) });
}

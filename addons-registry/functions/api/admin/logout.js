import { endedCookie, sameOrigin, reply } from '../../_lib/auth.js';

// POST /api/admin/logout : ferme la session.
export async function onRequestPost({ request }) {
  if (!sameOrigin(request)) return reply(403, { error: 'Requête refusée.' });
  return reply(200, { ok: true }, { 'set-cookie': endedCookie() });
}

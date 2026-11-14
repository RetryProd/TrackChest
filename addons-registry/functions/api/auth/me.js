import { reply } from '../../_lib/auth.js';
import { getUserSession, findUserByLogin, publicUser } from '../../_lib/users.js';

// GET /api/auth/me : renvoie l'utilisateur connecté (ou authenticated: false).
// Un compte supprimé n'est plus considéré comme connecté, même si son cookie signé n'a pas expiré.
export async function onRequestGet({ request, env }) {
  const session = await getUserSession(request, env);
  if (!session) return reply(200, { authenticated: false, user: null });

  let stored;
  try {
    stored = await findUserByLogin(env, session.usernameLower);
  } catch {
    // Lecture impossible pour le moment : on renvoie le profil minimal issu du cookie signé.
    return reply(200, { authenticated: true, user: { username: session.username, usernameLower: session.usernameLower, email: '' } });
  }
  if (!stored || stored.usernameLower !== session.usernameLower) return reply(200, { authenticated: false, user: null });
  return reply(200, { authenticated: true, user: publicUser(stored) });
}

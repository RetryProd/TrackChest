// En-têtes de sécurité ajoutés à TOUTES les réponses du site (pages statiques et fonctions).
//
// - Content-Security-Policy : seuls les scripts, styles et données du site lui-même sont chargés (pas de script en
//   ligne, pas de site tiers), la page ne peut pas être affichée dans un cadre d'un autre site (clic détourné sur
//   « Valider et publier »), et les formulaires ne peuvent envoyer que vers le site.
// - Strict-Transport-Security : le navigateur n'utilise plus que HTTPS pendant un an.
// - X-Content-Type-Options, Referrer-Policy, Permissions-Policy, Cross-Origin-Opener-Policy : protections classiques.
const CSP = [
  "default-src 'self'",
  "script-src 'self'",
  // 'unsafe-inline' pour les styles seulement : quelques attributs style="" dans les pages (aucun script en ligne).
  "style-src 'self' 'unsafe-inline'",
  "img-src 'self' data: blob:",
  "connect-src 'self'",
  "font-src 'self'",
  "object-src 'none'",
  "base-uri 'none'",
  "form-action 'self'",
  "frame-ancestors 'none'",
].join('; ');

const HEADERS = {
  'content-security-policy': CSP,
  'x-content-type-options': 'nosniff',
  'x-frame-options': 'DENY',
  'referrer-policy': 'strict-origin-when-cross-origin',
  'permissions-policy': 'camera=(), microphone=(), geolocation=(), payment=(), usb=(), interest-cohort=()',
  'cross-origin-opener-policy': 'same-origin',
  'cross-origin-resource-policy': 'same-origin',
};

export async function onRequest({ request, next }) {
  const response = await next();
  const secured = new Response(response.body, response);
  for (const [name, value] of Object.entries(HEADERS)) secured.headers.set(name, value);
  if (new URL(request.url).protocol === 'https:') {
    secured.headers.set('strict-transport-security', 'max-age=31536000; includeSubDomains');
  }
  const path = new URL(request.url).pathname;
  // Pages et API sensibles : jamais en cache, jamais indexées.
  if (path.startsWith('/api/') || path.startsWith('/admin')) {
    secured.headers.set('cache-control', 'no-store');
    secured.headers.set('x-robots-tag', 'noindex, nofollow');
  }
  return secured;
}

/**
 * Resolves the post-logout redirect URI for the game frontend.
 * @param origin Optional browser origin (for example window.location.origin).
 * @returns Absolute landing-page URI where logout should redirect.
 */
export function resolvePostLogoutRedirectUri(origin?: string) {
  if (!origin) {
    return 'http://localhost:5173/'
  }

  return `${origin}/`
}

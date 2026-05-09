export function resolvePostLogoutRedirectUri(origin?: string) {
  if (!origin) {
    return 'http://localhost:5173/'
  }

  return `${origin}/`
}

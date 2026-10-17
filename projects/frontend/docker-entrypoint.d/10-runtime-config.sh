#!/bin/sh
set -eu

HTML_DIR="${HTML_DIR:-/usr/share/nginx/html}"

cat > "$HTML_DIR/runtime-config.js" <<EOF
window.__capitalismRuntimeConfig__ = {
  graphqlUrl: "${APP_GRAPHQL_URL:-}",
  masterGraphqlUrl: "${APP_MASTER_GRAPHQL_URL:-}",
  masterWebUrl: "${APP_MASTER_WEB_URL:-}",
  biatecOidcAuthorizeUrl: "${APP_BIATEC_OIDC_AUTHORIZE_URL:-}",
  biatecOidcTokenUrl: "${APP_BIATEC_OIDC_TOKEN_URL:-}",
  biatecOidcEndSessionUrl: "${APP_BIATEC_OIDC_END_SESSION_URL:-}",
  biatecOidcClientId: "${APP_BIATEC_OIDC_CLIENT_ID:-}",
  biatecOidcRedirectUri: "${APP_BIATEC_OIDC_REDIRECT_URI:-}",
  biatecOidcScope: "${APP_BIATEC_OIDC_SCOPE:-}",
  biatecOidcAudience: "${APP_BIATEC_OIDC_AUDIENCE:-}",
  biatecOidcAllowedIssuers: "${APP_BIATEC_OIDC_ALLOWED_ISSUERS:-}",
  sentryDsn: "${APP_SENTRY_DSN:-}",
  googleAnalyticsId: "${APP_GOOGLE_ANALYTICS_ID:-}",
}
EOF

# Browsers that cached runtime-config.js under the old one-year rule would never revalidate it, so version the URL
# by content: the script tag in index.html changes whenever the generated settings change (idempotent across restarts).
version="$(cksum < "$HTML_DIR/runtime-config.js" | cut -d' ' -f1)"
sed -i -E "s#/runtime-config\.js(\?v=[0-9]+)?#/runtime-config.js?v=${version}#" "$HTML_DIR/index.html"

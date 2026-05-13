# Reverse proxy trust configuration (Api + MasterApi)

Both backends support safe forwarded-client-IP resolution through the `ReverseProxy` config section.

```json
"ReverseProxy": {
  "ForwardedForHopCount": 1,
  "TrustedProxies": [
    "127.0.0.1",
    "::1",
    "10.0.0.0/8"
  ]
}
```

- `ForwardedForHopCount`: max trusted `X-Forwarded-For` hops (`0` disables forwarded headers).
- `TrustedProxies`: direct proxy IPs or CIDR ranges that are allowed to supply `X-Forwarded-For`.
- If hop count is `0` or `TrustedProxies` is empty, forwarded-header processing is disabled and raw TCP `RemoteIpAddress` is used.

## nginx (single reverse proxy hop)

`nginx.conf`:

```nginx
location / {
    proxy_set_header Host $host;
    proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
    proxy_set_header X-Forwarded-Proto $scheme;
    proxy_pass http://game-api:44356;
}
```

Backend config (`appsettings` or env vars):

```json
"ReverseProxy": {
  "ForwardedForHopCount": 1,
  "TrustedProxies": ["10.10.0.15"]
}
```

## Cloudflare + origin proxy

When Cloudflare sits in front of your origin proxy/LB, trust the direct source range that connects to the app container/host.

Example:

```json
"ReverseProxy": {
  "ForwardedForHopCount": 1,
  "TrustedProxies": [
    "173.245.48.0/20",
    "103.21.244.0/22",
    "103.22.200.0/22",
    "103.31.4.0/22"
  ]
}
```

Use the currently published Cloudflare IP ranges for your deployment and keep this list updated.

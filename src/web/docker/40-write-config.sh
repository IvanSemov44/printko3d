#!/bin/sh
set -eu
: "${API_URL:?API_URL must be set}"
printf '{ "apiUrl": "%s" }\n' "$API_URL" > /usr/share/nginx/html/config.json

# Only the admin panel sets API_UPSTREAM. nginx then forwards /api/ to the API, so the browser talks to one
# address and the session cookie belongs to one site (decision 0014). Without it the file is empty.
PROXY_CONF=/tmp/api-proxy.conf
if [ -n "${API_UPSTREAM:-}" ]; then
    UPSTREAM="${API_UPSTREAM%/}"
    case "$UPSTREAM" in
        http://* | https://*) ;;
        *) echo "API_UPSTREAM must start with http:// or https://" >&2; exit 1 ;;
    esac
    case "$UPSTREAM" in
        *[!A-Za-z0-9.:/_-]*) echo "API_UPSTREAM contains characters that are not allowed" >&2; exit 1 ;;
    esac
    {
        printf 'location /api/ {\n'
        printf '    proxy_pass %s;\n' "$UPSTREAM"
        printf '    proxy_http_version 1.1;\n'
        printf '    proxy_set_header Host $proxy_host;\n'
        printf '    proxy_ssl_server_name on;\n'
        # The free plan on render.com wakes the API slowly after a pause.
        printf '    proxy_read_timeout 90s;\n'
        printf '}\n'
    } > "$PROXY_CONF"
else
    : > "$PROXY_CONF"
fi

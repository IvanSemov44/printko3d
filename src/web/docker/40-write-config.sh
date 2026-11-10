#!/bin/sh
set -eu
: "${API_URL:?API_URL must be set}"

# Optional: the public address that the images are served from (decision 0011). It goes into config.json, where the apps
# build the address of an image, and into the Content-Security-Policy, which would otherwise block the images.
# The value ends up in JSON and in the nginx configuration, so only plain address characters are allowed.
IMAGE_BASE="${IMAGE_BASE_URL:-}"
IMAGE_BASE="${IMAGE_BASE%/}"
if [ -n "$IMAGE_BASE" ]; then
    case "$IMAGE_BASE" in
        http://* | https://*) ;;
        *) echo "IMAGE_BASE_URL must start with http:// or https://" >&2; exit 1 ;;
    esac
    case "$IMAGE_BASE" in
        *[!A-Za-z0-9.:/_-]*) echo "IMAGE_BASE_URL contains characters that are not allowed" >&2; exit 1 ;;
    esac
    printf '{ "apiUrl": "%s", "imageBaseUrl": "%s" }\n' "$API_URL" "$IMAGE_BASE" > /usr/share/nginx/html/config.json
else
    printf '{ "apiUrl": "%s" }\n' "$API_URL" > /usr/share/nginx/html/config.json
fi

# default.conf.template reads $image_src. Without an image address it is empty and the policy stays as strict as before.
IMAGE_SRC_CONF=/tmp/image-src.conf
if [ -n "$IMAGE_BASE" ]; then
    printf 'set $image_src "%s/";\n' "$IMAGE_BASE" > "$IMAGE_SRC_CONF"
else
    printf 'set $image_src "";\n' > "$IMAGE_SRC_CONF"
fi

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
        # An image is up to 5 MB, and the form around it needs a little more (the API limit is 6 MB).
        printf '    client_max_body_size 6m;\n'
        printf '}\n'
    } > "$PROXY_CONF"
else
    : > "$PROXY_CONF"
fi

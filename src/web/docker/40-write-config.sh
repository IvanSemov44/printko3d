#!/bin/sh
set -eu
: "${API_URL:?API_URL must be set}"

# Where the files go. The defaults are the places in the image; a test can point them elsewhere.
WEB_ROOT="${WEB_ROOT:-/usr/share/nginx/html}"
CONF_DIR="${CONF_DIR:-/tmp}"

# A value that ends up in JSON and in the nginx configuration may only hold plain address characters.
check_address() {
    name="$1"
    value="$2"
    case "$value" in
        http://* | https://*) ;;
        *) echo "$name must start with http:// or https://" >&2; exit 1 ;;
    esac
    case "$value" in
        *[!A-Za-z0-9.:/_-]*) echo "$name contains characters that are not allowed" >&2; exit 1 ;;
    esac
}

# Optional: the public address that the images are served from (decision 0011). It goes into config.json, where the apps
# build the address of an image, and into the Content-Security-Policy, which would otherwise block the images.
IMAGE_BASE="${IMAGE_BASE_URL:-}"
IMAGE_BASE="${IMAGE_BASE%/}"
if [ -n "$IMAGE_BASE" ]; then
    check_address IMAGE_BASE_URL "$IMAGE_BASE"
fi

# Optional: the public address of the shop (the storefront only). It goes into config.json, where the shop builds its
# canonical and sharing addresses, and into robots.txt, for the address of the sitemap.
SITE="${SITE_URL:-}"
SITE="${SITE%/}"
if [ -n "$SITE" ]; then
    check_address SITE_URL "$SITE"
fi

{
    printf '{ "apiUrl": "%s"' "$API_URL"
    if [ -n "$IMAGE_BASE" ]; then printf ', "imageBaseUrl": "%s"' "$IMAGE_BASE"; fi
    if [ -n "$SITE" ]; then printf ', "siteUrl": "%s"' "$SITE"; fi
    printf ' }\n'
} > "$WEB_ROOT/config.json"

# default.conf.template reads $image_src. Without an image address it is empty and the policy stays as strict as before.
IMAGE_SRC_CONF="$CONF_DIR/image-src.conf"
if [ -n "$IMAGE_BASE" ]; then
    printf 'set $image_src "%s/";\n' "$IMAGE_BASE" > "$IMAGE_SRC_CONF"
else
    printf 'set $image_src "";\n' > "$IMAGE_SRC_CONF"
fi

# Only the admin panel sets API_UPSTREAM. nginx then forwards /api/ to the API, so the browser talks to one
# address and the session cookie belongs to one site (decision 0014). Without it the file is empty.
PROXY_CONF="$CONF_DIR/api-proxy.conf"
SITEMAP_CONF="$CONF_DIR/sitemap-proxy.conf"
CART_CONF="$CONF_DIR/cart-proxy.conf"
if [ -n "${API_UPSTREAM:-}" ]; then
    UPSTREAM="${API_UPSTREAM%/}"
    check_address API_UPSTREAM "$UPSTREAM"
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
    # The admin panel has no sitemap.
    : > "$SITEMAP_CONF"
    : > "$CART_CONF"
else
    : > "$PROXY_CONF"
    # The storefront serves /sitemap.xml from the API (the API builds it from the catalog), so a robot sees the
    # shop's own address.
    # SITEMAP_UPSTREAM is the address of the API as the container reaches it. Without it, the public one: that is enough on
    # render.com, but not in Docker, where "localhost" is the container itself.
    API_ADDRESS="${SITEMAP_UPSTREAM:-$API_URL}"
    API_ADDRESS="${API_ADDRESS%/}"
    check_address SITEMAP_UPSTREAM "$API_ADDRESS"
    {
        printf 'location = /sitemap.xml {\n'
        printf '    proxy_pass %s/sitemap.xml;\n' "$API_ADDRESS"
        printf '    proxy_http_version 1.1;\n'
        printf '    proxy_set_header Host $proxy_host;\n'
        printf '    proxy_ssl_server_name on;\n'
        printf '    proxy_read_timeout 90s;\n'
        printf '}\n'
    } > "$SITEMAP_CONF"
    # The cart cookie belongs to the shop's own address, so the browser reaches the cart through it (decision 0027).
    # The order is placed with the same cookie (decision 0032), so it goes the same way.
    # Only the cart and the orders are forwarded, never the admin addresses. With no URI in proxy_pass, nginx keeps the path as it came.
    {
        printf 'location ~ ^/api/v1/(cart|orders)(/|$) {\n'
        printf '    proxy_pass %s;\n' "$API_ADDRESS"
        printf '    proxy_http_version 1.1;\n'
        printf '    proxy_set_header Host $proxy_host;\n'
        printf '    proxy_ssl_server_name on;\n'
        printf '    proxy_read_timeout 90s;\n'
        printf '}\n'
    } > "$CART_CONF"
fi

# robots.txt. Search engines are kept out unless ALLOW_INDEXING=true, so a demo or a local copy is never indexed by
# accident. The admin panel is always kept out.
if [ "${ALLOW_INDEXING:-}" = "true" ] && [ -z "${API_UPSTREAM:-}" ]; then
    if [ -z "$SITE" ]; then
        echo "ALLOW_INDEXING=true needs SITE_URL, for the address of the sitemap" >&2
        exit 1
    fi
    printf 'User-agent: *\nAllow: /\n\nSitemap: %s/sitemap.xml\n' "$SITE" > "$WEB_ROOT/robots.txt"
else
    printf 'User-agent: *\nDisallow: /\n' > "$WEB_ROOT/robots.txt"
fi

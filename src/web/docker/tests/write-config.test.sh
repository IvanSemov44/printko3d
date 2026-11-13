#!/bin/sh
# Tests for 40-write-config.sh. Run: sh src/web/docker/tests/write-config.test.sh
# Each case runs the script in its own empty folder and checks the files it wrote.
set -u

SCRIPT="$(cd "$(dirname "$0")/.." && pwd)/40-write-config.sh"
FAILED=0

# run_case <name> <expected exit code> <env assignments...>; leaves the output in $OUT
run_case() {
    name="$1"; expected="$2"; shift 2
    OUT="$(mktemp -d)"
    mkdir "$OUT/html" "$OUT/conf"
    env -i PATH="$PATH" WEB_ROOT="$OUT/html" CONF_DIR="$OUT/conf" "$@" sh "$SCRIPT" >/dev/null 2>&1
    code=$?
    if [ "$code" -ne "$expected" ]; then
        echo "FAIL $name: exit code $code, expected $expected"
        FAILED=1
    fi
}

expect_file() { # <name> <file> <expected content, with \n>
    actual="$(cat "$2")"
    wanted="$(printf '%b' "$3")"
    if [ "$actual" != "$wanted" ]; then
        echo "FAIL $1: $2"
        echo "  expected: $wanted"
        echo "  actual:   $actual"
        FAILED=1
    fi
}

expect_contains() { # <name> <file> <text>
    if ! grep -qF -- "$3" "$2"; then
        echo "FAIL $1: $2 does not contain: $3"
        FAILED=1
    fi
}

expect_empty() { # <name> <file>
    if [ -s "$2" ]; then
        echo "FAIL $1: $2 should be empty"
        FAILED=1
    fi
}

run_case "storefront without extras" 0 API_URL=http://api.test
expect_file "config.json is as before" "$OUT/html/config.json" '{ "apiUrl": "http://api.test" }'
expect_file "robots.txt keeps everybody out by default" "$OUT/html/robots.txt" 'User-agent: *\nDisallow: /'
expect_contains "sitemap goes to the API" "$OUT/conf/sitemap-proxy.conf" "proxy_pass http://api.test/sitemap.xml;"
expect_empty "no admin proxy for the storefront" "$OUT/conf/api-proxy.conf"
expect_contains "cart goes to the API" "$OUT/conf/cart-proxy.conf" "proxy_pass http://api.test;"
expect_contains "only the cart and the orders are forwarded" "$OUT/conf/cart-proxy.conf" 'location ~ ^/api/v1/(cart|orders)(/|$) {'
if grep -qF "admin" "$OUT/conf/cart-proxy.conf"; then echo "FAIL cart proxy must not mention admin"; FAILED=1; fi

run_case "indexing allowed" 0 API_URL=http://api.test SITE_URL=https://shop.test/ ALLOW_INDEXING=true IMAGE_BASE_URL=http://img.test/
expect_file "config.json has all addresses" "$OUT/html/config.json" '{ "apiUrl": "http://api.test", "imageBaseUrl": "http://img.test", "siteUrl": "https://shop.test" }'
expect_file "robots.txt names the sitemap" "$OUT/html/robots.txt" 'User-agent: *\nAllow: /\n\nSitemap: https://shop.test/sitemap.xml'

run_case "site address without allowing indexing (the demo)" 0 API_URL=http://api.test SITE_URL=https://shop.test
expect_file "demo stays out of search engines" "$OUT/html/robots.txt" 'User-agent: *\nDisallow: /'
expect_contains "canonical address is still in config.json" "$OUT/html/config.json" '"siteUrl": "https://shop.test"'

run_case "allowing indexing needs the site address" 1 API_URL=http://api.test ALLOW_INDEXING=true

run_case "site address must be an address" 1 API_URL=http://api.test SITE_URL=shop.test
run_case "site address must be plain" 1 API_URL=http://api.test 'SITE_URL=https://shop.test/";x'

run_case "sitemap upstream differs from the public address of the API" 0 API_URL=http://localhost:8080 SITEMAP_UPSTREAM=http://api:8080/
expect_contains "sitemap goes to the address the container can reach" "$OUT/conf/sitemap-proxy.conf" "proxy_pass http://api:8080/sitemap.xml;"
expect_contains "cart goes to the address the container can reach" "$OUT/conf/cart-proxy.conf" "proxy_pass http://api:8080;"

run_case "admin panel" 0 API_URL=http://api.test API_UPSTREAM=http://api:8080/ ALLOW_INDEXING=true SITE_URL=https://shop.test
expect_file "admin panel is never indexed" "$OUT/html/robots.txt" 'User-agent: *\nDisallow: /'
expect_empty "admin panel has no sitemap" "$OUT/conf/sitemap-proxy.conf"
expect_empty "admin panel has no cart proxy" "$OUT/conf/cart-proxy.conf"
expect_contains "admin panel forwards /api/" "$OUT/conf/api-proxy.conf" "proxy_pass http://api:8080;"

if [ "$FAILED" -eq 0 ]; then
    echo "write-config: all cases passed"
fi
exit "$FAILED"

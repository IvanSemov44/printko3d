#!/bin/sh
set -eu
: "${API_URL:?API_URL must be set}"
printf '{ "apiUrl": "%s" }\n' "$API_URL" > /usr/share/nginx/html/config.json

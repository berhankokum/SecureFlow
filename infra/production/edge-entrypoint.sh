#!/bin/sh
set -eu
case "${SERVER_NAME:-}" in
  ''|*[!a-zA-Z0-9.-]*|.*|*.) echo 'SERVER_NAME must be an explicit hostname' >&2; exit 1 ;;
esac
case "${HTTPS_PORT:-}" in
  ''|*[!0-9]*) echo 'HTTPS_PORT must be numeric' >&2; exit 1 ;;
esac
[ "$HTTPS_PORT" -ge 1 ] && [ "$HTTPS_PORT" -le 65535 ] || exit 1
envsubst '${SERVER_NAME} ${HTTPS_PORT}' < /etc/nginx/site.conf.template > /tmp/site.conf
nginx -t
exec nginx -g 'daemon off;'

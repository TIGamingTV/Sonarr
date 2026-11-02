#!/bin/sh
set -e

umask "${UMASK:-002}"

SONARR="/app/sonarr/bin/Sonarr"
ARGS="-nobrowser -data=/config"

if [ "$(id -u)" = "0" ]; then
    PUID="${PUID:-1000}"
    PGID="${PGID:-1000}"

    mkdir -p /config

    # Only fix ownership if needed, avoids walking large config folders on every start
    if [ "$(stat -c '%u:%g' /config)" != "${PUID}:${PGID}" ]; then
        echo "Setting ownership of /config to ${PUID}:${PGID}"
        chown -R "${PUID}:${PGID}" /config
    fi

    # shellcheck disable=SC2086
    exec setpriv --reuid="${PUID}" --regid="${PGID}" --clear-groups \
        env HOME=/config "$SONARR" $ARGS "$@"
fi

# shellcheck disable=SC2086
exec "$SONARR" $ARGS "$@"

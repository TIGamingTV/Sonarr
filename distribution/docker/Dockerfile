# Runtime image for Sonarr. The binaries are built by .github/workflows/docker.yml and
# placed in the build context as <arch>/bin (Sonarr build + UI) and <arch>/package_info.
FROM mcr.microsoft.com/dotnet/runtime-deps:10.0

ARG TARGETARCH
ARG VERSION=""

LABEL org.opencontainers.image.title="Sonarr" \
      org.opencontainers.image.description="Sonarr with TMDB as an additional metadata source" \
      org.opencontainers.image.licenses="GPL-3.0" \
      org.opencontainers.image.version="${VERSION}"

ENV PUID=1000 \
    PGID=1000 \
    UMASK=002 \
    TZ=Etc/UTC

RUN apt-get update && \
    apt-get install -y --no-install-recommends tzdata sqlite3 curl && \
    rm -rf /var/lib/apt/lists/*

COPY ${TARGETARCH}/ /app/sonarr/
COPY entrypoint.sh /entrypoint.sh

RUN chmod +x /entrypoint.sh /app/sonarr/bin/Sonarr && \
    find /app/sonarr/bin -name ffprobe -exec chmod +x {} \; && \
    mkdir -p /config

EXPOSE 8989
VOLUME ["/config"]

HEALTHCHECK --interval=1m --timeout=10s --start-period=2m --retries=3 \
    CMD curl -fsS "http://localhost:8989/ping" > /dev/null || exit 1

ENTRYPOINT ["/entrypoint.sh"]

# Kong API Gateway (OSS, DB-less). Build context: repository root
#   docker build -f gateway/kong/Dockerfile -t tuition-kong .
FROM kong:3.9.1

USER root
RUN mkdir -p /kong && chown kong:kong /kong
COPY --chown=kong:kong gateway/kong/kong.yml.template /kong/kong.yml.template
COPY --chown=kong:kong --chmod=755 gateway/kong/render-and-start.sh /kong/render-and-start.sh
USER kong

ENV KONG_DATABASE=off \
    KONG_DECLARATIVE_CONFIG=/kong/kong.yml \
    KONG_PROXY_LISTEN="0.0.0.0:8000" \
    KONG_ADMIN_LISTEN=off \
    KONG_PROXY_ACCESS_LOG=/dev/stdout \
    KONG_PROXY_ERROR_LOG=/dev/stderr \
    KONG_ADMIN_ERROR_LOG=/dev/stderr \
    KONG_TRUSTED_IPS="0.0.0.0/0,::/0" \
    KONG_REAL_IP_HEADER=X-Forwarded-For \
    KONG_PLUGINS=bundled

EXPOSE 8000
HEALTHCHECK --interval=10s --timeout=3s --start-period=10s --retries=5 CMD kong health
ENTRYPOINT ["/kong/render-and-start.sh"]

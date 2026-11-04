# Build context is src/web: docker build -f docker/Dockerfile --build-arg APP=storefront src/web
FROM node:26.13.0-alpine AS build
ARG APP
WORKDIR /web
COPY package.json package-lock.json ./
COPY shared/package.json shared/
COPY storefront/package.json storefront/
COPY admin/package.json admin/
RUN npm ci
COPY shared shared
COPY ${APP} ${APP}
RUN npm run build --workspace @storefront/${APP}

FROM nginxinc/nginx-unprivileged:1.29.3-alpine AS runtime
ARG APP
COPY --from=build --chown=101:101 /web/${APP}/dist /usr/share/nginx/html
COPY docker/default.conf.template /etc/nginx/templates/default.conf.template
COPY --chmod=755 docker/40-write-config.sh /docker-entrypoint.d/40-write-config.sh
USER root
RUN chown 101:101 /usr/share/nginx/html
USER 101
EXPOSE 8080
HEALTHCHECK --interval=15s --timeout=5s --start-period=5s --retries=5 \
    CMD wget -q --spider http://127.0.0.1:8080/ || exit 1

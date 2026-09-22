FROM node:24-alpine@sha256:ebfe2f90462722a7a4de65e91990e97fe0d401c70e0e762c5b53302f905ec1c1 AS build
WORKDIR /source
COPY frontend/package*.json ./
RUN npm ci
COPY frontend/ ./
RUN npm run build

FROM nginx:stable-alpine@sha256:ef8676b33d681f272ba429b27658bdd7e640963279714c96bddf1dc76307f7b6
COPY --from=build /source/dist/ /usr/share/nginx/html/
COPY infra/production/nginx.conf /etc/nginx/nginx.conf
COPY infra/production/security-headers.conf /etc/nginx/security-headers.conf
COPY infra/production/api-security-headers.conf /etc/nginx/api-security-headers.conf
COPY infra/production/site.conf.template /etc/nginx/site.conf.template
COPY --chmod=755 infra/production/edge-entrypoint.sh /edge-entrypoint.sh
RUN sed -i 's/\r$//' /edge-entrypoint.sh
USER 101:101
EXPOSE 8080 8443
HEALTHCHECK --interval=15s --timeout=5s --start-period=10s CMD wget -q -O /dev/null http://127.0.0.1:8080/health/live || exit 1
ENTRYPOINT ["/edge-entrypoint.sh"]

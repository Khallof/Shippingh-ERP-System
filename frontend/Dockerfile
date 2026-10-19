# ShippingERP web app: build with Node, serve the static files with an unprivileged nginx.
# Build context = the frontend folder:  docker build -t shippingerp-web ./frontend

# ---------- Build stage ----------
FROM node:20.18.1-alpine AS build
WORKDIR /app

# Dependencies first (cached while package*.json do not change). npm ci = exact versions of package-lock.json.
COPY package.json package-lock.json ./
RUN npm ci --no-audit --no-fund

COPY . .
# Type check + production bundle (dist/). VITE_* values are baked into the bundle at build time.
ARG VITE_API_BASE_URL=/api/v1
ARG VITE_BUSINESS_TIME_ZONE=Asia/Damascus
ENV VITE_API_BASE_URL=$VITE_API_BASE_URL VITE_BUSINESS_TIME_ZONE=$VITE_BUSINESS_TIME_ZONE
RUN npm run build

# ---------- Runtime ----------
# Runs as a non-root user and listens on 8080.
FROM nginxinc/nginx-unprivileged:1.27.3-alpine AS final

# The template is rendered at start-up (envsubst): API_UPSTREAM = where /api is forwarded.
ENV API_UPSTREAM=api:8080
COPY nginx/default.conf.template /etc/nginx/templates/default.conf.template
COPY --from=build /app/dist /usr/share/nginx/html

EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
  CMD wget -q -O /dev/null http://127.0.0.1:8080/healthz || exit 1

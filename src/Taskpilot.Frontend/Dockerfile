# Multi-stage build for the Taskpilot React/Vite frontend.
# Build context is the frontend folder (see docker-compose.yml).

# ---- Build stage ----
FROM node:22-alpine AS build
WORKDIR /app

# Install dependencies first (cached unless the lock file changes).
COPY package.json package-lock.json ./
RUN npm ci

# Bake the API base URL into the bundle. Empty means "use relative URLs", which
# works because nginx reverse-proxies /api and /hubs to the backend (same origin).
ARG VITE_API_URL=""
RUN printf "VITE_API_URL=%s\n" "$VITE_API_URL" > .env.production

# Copy the rest of the source and produce the static build.
COPY . .
RUN npm run build

# ---- Runtime stage ----
FROM nginx:alpine AS final
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist /usr/share/nginx/html
EXPOSE 80

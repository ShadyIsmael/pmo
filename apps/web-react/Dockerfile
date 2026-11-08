FROM node:22.23.4-alpine AS deps
WORKDIR /app
COPY package.json package-lock.json ./
RUN npm ci

FROM node:22.23.4-alpine AS builder
WORKDIR /app
ARG VITE_API_URL=
ARG VITE_FIREBASE_API_KEY=
ARG VITE_FIREBASE_AUTH_DOMAIN=
ARG VITE_FIREBASE_PROJECT_ID=
ARG VITE_FIREBASE_STORAGE_BUCKET=
ARG VITE_FIREBASE_MESSAGING_SENDER_ID=
ARG VITE_FIREBASE_APP_ID=
ARG VITE_FIREBASE_VAPID_KEY=
ENV VITE_API_URL=${VITE_API_URL}
ENV VITE_FIREBASE_API_KEY=${VITE_FIREBASE_API_KEY}
ENV VITE_FIREBASE_AUTH_DOMAIN=${VITE_FIREBASE_AUTH_DOMAIN}
ENV VITE_FIREBASE_PROJECT_ID=${VITE_FIREBASE_PROJECT_ID}
ENV VITE_FIREBASE_STORAGE_BUCKET=${VITE_FIREBASE_STORAGE_BUCKET}
ENV VITE_FIREBASE_MESSAGING_SENDER_ID=${VITE_FIREBASE_MESSAGING_SENDER_ID}
ENV VITE_FIREBASE_APP_ID=${VITE_FIREBASE_APP_ID}
ENV VITE_FIREBASE_VAPID_KEY=${VITE_FIREBASE_VAPID_KEY}
COPY --from=deps /app/node_modules ./node_modules
COPY . .
RUN npm run build

FROM nginx:1.27-alpine AS runner
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=builder /app/dist /usr/share/nginx/html
RUN sed -i 's#pid /var/run/nginx.pid;#pid /var/cache/nginx/nginx.pid;#' /etc/nginx/nginx.conf \
    && grep -q 'pid /var/cache/nginx/nginx.pid;' /etc/nginx/nginx.conf \
    && chown -R nginx:nginx /var/cache/nginx
USER nginx
EXPOSE 80
ENTRYPOINT ["nginx"]
CMD ["-g", "daemon off;"]

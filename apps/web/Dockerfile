# RAGence 前端（Next.js standalone）。建置 context 必須是 repo 根目錄，因為要一併建置 packages/rag-client-sdk：
#   docker build --platform linux/amd64 -f apps/web/Dockerfile \
#     --build-arg NEXT_PUBLIC_SITE_URL=https://<網域> -t rag-web:<tag> .
# 容器內只聽 8005（不使用 80 / 443），由 Nginx 以容器名稱轉發。
# NEXT_PUBLIC_* 會在建置時寫進前端程式，所以要在 build 時給；NEXT_PUBLIC_RAG_API_URL 留空 = 與網站同網域。

FROM node:22-alpine AS build
WORKDIR /repo
ARG NEXT_PUBLIC_SITE_URL=""
ARG NEXT_PUBLIC_RAG_API_URL=""
ENV NEXT_PUBLIC_SITE_URL=$NEXT_PUBLIC_SITE_URL \
    NEXT_PUBLIC_RAG_API_URL=$NEXT_PUBLIC_RAG_API_URL \
    NEXT_TELEMETRY_DISABLED=1

# 先建置 SDK（前端以 file: 相依，使用它的 dist）
COPY packages/rag-client-sdk/package.json packages/rag-client-sdk/package-lock.json packages/rag-client-sdk/
RUN npm --prefix packages/rag-client-sdk ci --no-audit --no-fund
COPY packages/rag-client-sdk packages/rag-client-sdk
RUN npm --prefix packages/rag-client-sdk run build

COPY apps/web/package.json apps/web/package-lock.json apps/web/
RUN npm --prefix apps/web ci --no-audit --no-fund
COPY apps/web apps/web
# SDK 已建置完成，直接 next build（略過 package.json 的 prebuild）
RUN cd apps/web && npx next build --turbopack

FROM node:22-alpine AS final
WORKDIR /app
ENV NODE_ENV=production \
    PORT=8005 \
    HOSTNAME=0.0.0.0 \
    NEXT_TELEMETRY_DISABLED=1
# standalone 輸出以 repo 根目錄為基準：server.js 在 apps/web/ 底下
COPY --from=build --chown=node:node /repo/apps/web/.next/standalone ./
COPY --from=build --chown=node:node /repo/apps/web/.next/static ./apps/web/.next/static
COPY --from=build --chown=node:node /repo/apps/web/public ./apps/web/public
USER node
EXPOSE 8005
HEALTHCHECK --interval=30s --timeout=5s --start-period=20s --retries=3 \
  CMD wget -qO- http://127.0.0.1:8005/about >/dev/null || exit 1
CMD ["node", "apps/web/server.js"]

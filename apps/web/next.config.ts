import path from "node:path";
import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  // SDK 以 file: 連結自 packages/rag-client-sdk（編譯輸出在 dist），需把 repo 根目錄納入 bundler 範圍
  devIndicators: false,
  // 容器部署：輸出自帶相依套件的 standalone 伺服器（見 Dockerfile）
  output: "standalone",
  transpilePackages: ["@rag/client-sdk"],
  turbopack: { root: path.resolve(__dirname, "../..") },
  outputFileTracingRoot: path.resolve(__dirname, "../.."),
  async headers() {
    return [
      {
        // Service Worker 本身不可被瀏覽器 / CDN 長期快取，否則更新版本時使用者拿不到新的 sw.js
        source: "/sw.js",
        headers: [
          { key: "Cache-Control", value: "no-cache, no-store, must-revalidate" },
          { key: "Service-Worker-Allowed", value: "/" },
        ],
      },
    ]
  },
};

export default nextConfig;

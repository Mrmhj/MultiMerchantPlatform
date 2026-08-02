import { defineConfig } from "vite";
import uni from "@dcloudio/vite-plugin-uni";

// https://vitejs.dev/config/
export default defineConfig({
  plugins: [uni()],
  server: {
    port: 5175,
    proxy: {
      // H5 开发环境：REST + SignalR WebSocket 代理到 YARP 网关（8000）
      "/api": {
        target: "http://localhost:8000",
        changeOrigin: true,
      },
      "/hub": {
        target: "http://localhost:8000",
        changeOrigin: true,
        ws: true,
      },
    },
  },
});

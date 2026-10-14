import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'

// KnowVault 前端（Vue 3 + JS）。开发时把 /api 与 /connect 代理到后端：
// - /api     业务接口
// - /connect OpenIddict 令牌端点（密码模式换 token），必须同源代理，否则要同时处理 CORS 与自签证书
export default defineConfig({
  plugins: [vue()],
  server: {
    port: 5173,
    proxy: {
      '/api': {
        // ABP HttpApi.Host 默认 https://localhost:44300；用 Aspire 编排时其端口已钉为 44300
        target: process.env.VITE_API_TARGET || 'https://localhost:44300',
        changeOrigin: true,
        secure: false,
      },
      '/signalr': {
        // SignalR（WebSocket）：必须显式开 ws，否则 Vite 只做普通 HTTP 代理，握手直接失败
        target: process.env.VITE_API_TARGET || 'https://localhost:44300',
        changeOrigin: true,
        secure: false,
        ws: true,
      },
      '/connect': {
        target: process.env.VITE_API_TARGET || 'https://localhost:44300',
        changeOrigin: true,
        secure: false,
      },
    },
  },
})

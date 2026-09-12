import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'

// KnowVault 前端（Vue 3 + JS）。开发时把 /api 代理到后端，避免跨域与 Key 泄露。
export default defineConfig({
  plugins: [vue()],
  server: {
    port: 5173,
    proxy: {
      '/api': {
        // ABP HttpApi.Host 默认 https://localhost:44300；用 Aspire 编排时按其分配的端口改为 http。
        target: process.env.VITE_API_TARGET || 'https://localhost:44300',
        changeOrigin: true,
        secure: false,
      },
    },
  },
})

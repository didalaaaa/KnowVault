import axios from 'axios'

// 统一 axios 实例：baseURL=/api（开发时由 Vite 代理到后端），统一拦截器处理。
export const http = axios.create({
  baseURL: '/api',
  timeout: 30000,
})

http.interceptors.response.use(
  (res) => res.data,
  (err) => {
    // P0 骨架：统一打印错误；后续可扩展为全局提示。
    console.error('[KnowVault] 请求错误', err)
    return Promise.reject(err)
  },
)

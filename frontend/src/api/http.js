import axios from 'axios'

const TOKEN_KEY = 'knowvault.token'

// 统一 axios 实例：baseURL=/api（开发时由 Vite 代理到后端），统一拦截器处理鉴权与错误。
export const http = axios.create({
  baseURL: '/api',
  timeout: 60000, // 问答链路要调两次外部模型，30s 偏紧
})

http.interceptors.request.use((config) => {
  const token = localStorage.getItem(TOKEN_KEY)
  if (token) {
    config.headers.Authorization = `Bearer ${token}`
  }
  return config
})

http.interceptors.response.use(
  (res) => res.data,
  (err) => {
    // 401：令牌缺失/过期 → 清掉本地令牌，让界面回到登录页
    if (err?.response?.status === 401) {
      localStorage.removeItem(TOKEN_KEY)
      window.dispatchEvent(new CustomEvent('knowvault:unauthorized'))
    }
    console.error('[KnowVault] 请求错误', err)
    return Promise.reject(err)
  },
)

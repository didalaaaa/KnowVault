import axios from 'axios'

const TOKEN_KEY = 'knowvault.token'

/**
 * 取本地访问令牌（SignalR 的 accessTokenFactory 也要用它）。
 * @returns {string|null} 令牌，未登录时为 null
 */
export function getToken() {
  return localStorage.getItem(TOKEN_KEY)
}

// 统一 axios 实例：baseURL=/api（开发时由 Vite 代理到后端），统一拦截器处理鉴权与错误。
export const http = axios.create({
  baseURL: '/api',
  timeout: 60000, // 问答链路要调两次外部模型，30s 偏紧
})

http.interceptors.request.use((config) => {
  const token = getToken()
  if (token) {
    config.headers.Authorization = `Bearer ${token}`
  }

  // ABP 的 CSRF 防护：浏览器带上 XSRF-TOKEN Cookie 后，写请求必须回传同名令牌头，
  // 否则上传等 POST 会被 400 拒绝（curl 没有 Cookie，所以不会触发）
  const xsrf = document.cookie
    .split('; ')
    .find((row) => row.startsWith('XSRF-TOKEN='))
    ?.slice('XSRF-TOKEN='.length)
  if (xsrf) {
    config.headers.RequestVerificationToken = decodeURIComponent(xsrf)
  }
  return config
})

http.interceptors.response.use(
  (res) => res.data,
  (err) => {
    // 401：令牌缺失/过期 → 清掉本地令牌，让界面回到登录页。
    // 只处理"带着令牌却被拒绝"的请求：未登录时发出的旧请求（如退出后残留的轮询）
    // 可能在新会话登录之后才返回 401，不能拿它把刚建立的会话踢下线
    const sentAuth = Boolean(err?.config?.headers?.Authorization)
    if (err?.response?.status === 401 && sentAuth) {
      localStorage.removeItem(TOKEN_KEY)
      window.dispatchEvent(new CustomEvent('knowvault:unauthorized'))
    }
    console.error('[KnowVault] 请求错误', err)
    return Promise.reject(err)
  },
)

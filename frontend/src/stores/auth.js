import { defineStore } from 'pinia'
import { login as loginApi } from '../api/auth'
import { accountApi } from '../api/account'

const TOKEN_KEY = 'knowvault.token'

// 登录态：token 存 localStorage，刷新页面不丢；资料按需拉取后常驻内存。
export const useAuthStore = defineStore('auth', {
  state: () => ({
    token: localStorage.getItem(TOKEN_KEY) || '',
    profile: null,
    loading: false,
    error: '',
  }),

  getters: {
    /**
     * 是否已登录。
     * @returns {boolean} 有 token 即视为已登录
     */
    isAuthenticated: (state) => !!state.token,

    /**
     * header 展示的用户名：优先显示名，其次登录名。
     * @returns {string} 展示名
     */
    displayName: (state) => state.profile?.displayName || state.profile?.userName || '',
  },

  actions: {
    /**
     * 登录并保存令牌。
     * @param {{username: string, password: string}} credentials 登录凭据
     * @returns {Promise<boolean>} 是否登录成功
     */
    async login(credentials) {
      this.loading = true
      this.error = ''
      try {
        const { accessToken } = await loginApi(credentials)
        this.token = accessToken
        this.profile = null
        localStorage.setItem(TOKEN_KEY, accessToken)
        return true
      } catch (e) {
        // OpenIddict 失败时返回 { error: 'invalid_grant', error_description: '...' }，英文文案不直接展示给用户
        const data = e?.response?.data
        this.error = data?.error === 'invalid_grant' ? '用户名或密码错误' : data?.error_description || e?.message || '登录失败'
        return false
      } finally {
        this.loading = false
      }
    },

    /** 拉取当前用户资料；失败不打断页面（401 由 http 拦截器统一处理）。 */
    async loadProfile() {
      if (!this.token) return
      try {
        this.profile = await accountApi.getProfile()
      } catch {
        this.profile = null
      }
    },

    /**
     * 用后端返回的资料覆盖本地缓存。
     * @param {object|null} profile 用户资料
     */
    setProfile(profile) {
      this.profile = profile
    },

    /** 退出登录并清理本地令牌与资料。 */
    logout() {
      this.token = ''
      this.profile = null
      localStorage.removeItem(TOKEN_KEY)
      // 防伪令牌绑定了当前用户声明，退出后不清掉会让匿名的注册请求被 400 拒绝
      document.cookie = 'XSRF-TOKEN=; Max-Age=0; path=/'
    },
  },
})

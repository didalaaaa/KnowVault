import { defineStore } from 'pinia'
import { login as loginApi } from '../api/auth'

const TOKEN_KEY = 'knowvault.token'

// 登录态：token 存 localStorage，刷新页面不丢；refresh token 自动续期留 P2。
export const useAuthStore = defineStore('auth', {
  state: () => ({
    token: localStorage.getItem(TOKEN_KEY) || '',
    loading: false,
    error: '',
  }),

  getters: {
    /**
     * 是否已登录。
     * @returns {boolean} 有 token 即视为已登录
     */
    isAuthenticated: (state) => !!state.token,
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
        localStorage.setItem(TOKEN_KEY, accessToken)
        return true
      } catch (e) {
        // OpenIddict 失败时返回 { error: 'invalid_grant', error_description: '...' }
        this.error = e?.response?.data?.error_description || e?.message || '登录失败'
        return false
      } finally {
        this.loading = false
      }
    },

    /** 退出登录并清理本地令牌。 */
    logout() {
      this.token = ''
      localStorage.removeItem(TOKEN_KEY)
    },
  },
})

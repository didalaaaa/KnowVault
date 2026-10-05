import axios from 'axios'

// OpenIddict 令牌端点：走 Vite 的 /connect 代理，避免跨域与自签证书问题。
const TOKEN_URL = '/connect/token'
const CLIENT_ID = 'KnowVault_App'

/**
 * 用资源所有者密码模式换取访问令牌。
 * @param {{username: string, password: string}} credentials 登录凭据
 * @returns {Promise<{accessToken: string, refreshToken: string|null, expiresIn: number}>} 令牌信息
 */
export async function login({ username, password }) {
  const body = new URLSearchParams({
    grant_type: 'password',
    client_id: CLIENT_ID,
    username,
    password,
    scope: 'openid profile email roles KnowVault offline_access',
  })

  // 令牌端点不在 /api 下，因此不能复用带 baseURL 的 http 实例
  const { data } = await axios.post(TOKEN_URL, body, {
    headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
  })

  return {
    accessToken: data.access_token,
    refreshToken: data.refresh_token || null,
    expiresIn: data.expires_in,
  }
}

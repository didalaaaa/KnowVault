import { http } from './http'

// 账户接口：注册、资料、改密（ABP 动态 API，路由由方法名约定生成）
export const accountApi = {
  /**
   * 注册新账号（匿名接口），成功后默认加入 Member 角色。
   * @param {{userName: string, email: string, password: string, displayName?: string}} payload 注册信息
   * @returns {Promise<object>} 新用户资料
   */
  register: (payload) => http.post('/app/account/register', payload),

  /**
   * 获取当前登录用户资料。
   * @returns {Promise<object>} 用户资料（含角色）
   */
  getProfile: () => http.get('/app/account/profile'),

  /**
   * 修改显示名与邮箱。
   * @param {{displayName?: string, email: string}} payload 资料
   * @returns {Promise<object>} 更新后的资料
   */
  updateProfile: (payload) => http.put('/app/account/profile', payload),

  /**
   * 修改密码。
   * @param {{currentPassword: string, newPassword: string}} payload 原密码与新密码
   * @returns {Promise<void>}
   */
  changePassword: (payload) => http.post('/app/account/change-password', payload),
}

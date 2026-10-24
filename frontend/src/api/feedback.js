import { http } from './http'

// 消息反馈接口（ABP 动态 API：POST /api/app/feedback/set）
export const feedbackApi = {
  /**
   * 设置、切换或取消对某条助手消息的反馈。
   * @param {{messageId: string, rating: number|null, comment?: string}} payload 反馈内容；rating 为 null 表示取消
   * @returns {Promise<number|null>} 保存后的评价类型
   */
  set: (payload) => http.post('/app/feedback/set', payload),
}

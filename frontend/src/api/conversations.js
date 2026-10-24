import { http } from './http'

// 会话接口：列表 / 消息 / 重命名 / 删除
export const conversationApi = {
  /**
   * 列出当前用户的会话。
   * @param {string} [knowledgeBaseId] 按知识库过滤
   * @returns {Promise<Array>} 会话摘要
   */
  list: (knowledgeBaseId) =>
    http.get('/app/conversation', { params: knowledgeBaseId ? { knowledgeBaseId } : {} }),

  /**
   * 取会话的全部消息。
   * @param {string} id 会话 ID
   * @returns {Promise<Array>} 消息列表
   */
  messages: (id) => http.get(`/app/conversation/${id}/messages`),

  /**
   * 重命名会话（ABP 约定 PUT /api/app/conversation，ID 在请求体）。
   * @param {{id: string, title: string}} payload 会话 ID 与新标题
   * @returns {Promise<object>} 更新后的会话摘要
   */
  rename: (payload) => http.put('/app/conversation', payload),

  /**
   * 删除会话。
   * @param {string} id 会话 ID
   */
  remove: (id) => http.delete(`/app/conversation/${id}`),
}

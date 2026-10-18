import { http } from './http'

// 知识库成员接口（ABP 动态 API：服务名去掉 AppService 后缀 → 单数 /api/app/knowledge-base-member）
export const knowledgeBaseMemberApi = {
  /**
   * 取知识库成员列表。
   * @param {string} knowledgeBaseId 知识库 ID
   * @returns {Promise<object[]>} 成员列表
   */
  list: (knowledgeBaseId) => http.get('/app/knowledge-base-member', { params: { knowledgeBaseId } }),

  /**
   * 新增成员或调整角色。
   * @param {{knowledgeBaseId: string, userId: string, role: number}} payload 成员与角色
   * @returns {Promise<object>} 成员
   */
  set: (payload) => http.post('/app/knowledge-base-member', payload),

  /**
   * 移除成员。
   * @param {string} knowledgeBaseId 知识库 ID
   * @param {string} userId 用户 ID
   * @returns {Promise<void>}
   */
  remove: (knowledgeBaseId, userId) =>
    http.delete('/app/knowledge-base-member/' + knowledgeBaseId + '/' + userId),
}

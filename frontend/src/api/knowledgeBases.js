import { http } from './http'

// 知识库接口（P2：列表 + 默认库 + 增改删）
export const knowledgeBaseApi = {
  /**
   * 列出知识库。
   * @returns {Promise<object[]>} 知识库列表（默认知识库在最前）
   */
  list: () => http.get('/app/knowledge-base'),

  /**
   * 取默认知识库。
   * @returns {Promise<object>} 默认知识库
   */
  getDefault: () => http.get('/app/knowledge-base/default'),

  /**
   * 新建知识库。
   * @param {{name: string, description?: string}} payload 名称与描述
   * @returns {Promise<object>} 新建的知识库
   */
  create: (payload) => http.post('/app/knowledge-base', payload),

  /**
   * 修改知识库。
   * 注意：ABP 把 UpdateAsync(输入DTO) 映射成不带 {id} 的 PUT 路由，知识库 ID 走请求体。
   * @param {{id: string, name: string, description?: string}} payload 知识库 ID 与新名称、描述
   * @returns {Promise<object>} 修改后的知识库
   */
  update: (payload) => http.put('/app/knowledge-base', payload),

  /**
   * 删除知识库（后端只允许空库、默认库不可删）。
   * @param {string} id 知识库 ID
   * @returns {Promise<void>}
   */
  remove: (id) => http.delete('/app/knowledge-base/' + id),
}

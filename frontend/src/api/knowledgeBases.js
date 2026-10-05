import { http } from './http'

// 知识库接口（P1 只有列表与默认库）
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
}

import { http } from './http'

// 文档接口（ABP 动态 API：服务名去掉 AppService 后缀 → 单数 /api/app/document）
export const documentApi = {
  /**
   * 上传文档。
   * @param {{knowledgeBaseId: string, title?: string, file: File}} payload 上传参数
   * @returns {Promise<object>} 新建的文档 DTO
   */
  upload: ({ knowledgeBaseId, title, file }) => {
    const form = new FormData()
    form.append('knowledgeBaseId', knowledgeBaseId)
    if (title) form.append('title', title)
    form.append('file', file)
    return http.post('/app/document/upload', form, {
      headers: { 'Content-Type': 'multipart/form-data' },
    })
  },

  /**
   * 按知识库列文档。
   * @param {string} knowledgeBaseId 知识库 ID
   * @returns {Promise<object[]>} 文档列表
   */
  list: (knowledgeBaseId) => http.get('/app/document', { params: { knowledgeBaseId } }),
}

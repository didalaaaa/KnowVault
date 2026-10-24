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

  /**
   * 删除文档（后端同时清理切片、向量与原件）。
   * @param {string} id 文档 ID
   * @returns {Promise<void>}
   */
  remove: (id) => http.delete('/app/document/' + id),

  /**
   * 重新处理文档（后端清旧切片/向量后重新排队）。
   * @param {string} id 文档 ID
   * @returns {Promise<void>}
   */
  reprocess: (id) => http.post('/app/document/' + id + '/reprocess'),

  /**
   * 取文档切片（按序号升序）。
   * @param {string} id 文档 ID
   * @returns {Promise<object[]>} 切片列表
   */
  getChunks: (id) => http.get('/app/document/' + id + '/chunks'),

  /**
   * 取文档原件；需带 Bearer，故以 blob 返回再由前端生成 ObjectURL。
   * @param {string} id 文档 ID
   * @param {boolean} download 是否按附件下载
   * @returns {Promise<Blob>} 原件二进制
   */
  content: (id, download = false) =>
    http.get('/app/document/' + id + '/content', {
      responseType: 'blob',
      params: download ? { download: true } : undefined,
    }),
}

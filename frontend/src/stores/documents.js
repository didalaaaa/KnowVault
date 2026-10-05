import { defineStore } from 'pinia'
import { documentApi } from '../api/documents'
import { knowledgeBaseApi } from '../api/knowledgeBases'

// 文档与知识库状态。轮询只在存在未完成文档时进行，避免空转。
export const useDocumentStore = defineStore('documents', {
  state: () => ({
    knowledgeBases: [],
    knowledgeBaseId: '',
    documents: [],
    uploading: false,
    polling: false,
    error: '',
  }),

  actions: {
    /** 初始化：取知识库列表并选中默认知识库（后端已把默认库排在最前）。 */
    async init() {
      try {
        this.knowledgeBases = await knowledgeBaseApi.list()
        this.knowledgeBaseId = this.knowledgeBases[0]?.id || ''
        await this.refresh()
      } catch (e) {
        this.error = e?.response?.data?.error?.message || e?.message || '加载知识库失败'
      }
    },

    /** 拉取当前知识库的文档列表。 */
    async refresh() {
      if (!this.knowledgeBaseId) return
      try {
        this.documents = await documentApi.list(this.knowledgeBaseId)
        this.ensurePolling()
      } catch (e) {
        this.error = e?.response?.data?.error?.message || e?.message || '加载文档失败'
      }
    },

    /**
     * 上传文件并刷新列表。
     * @param {File} file 待上传文件
     * @returns {Promise<boolean>} 是否上传成功
     */
    async upload(file) {
      this.uploading = true
      this.error = ''
      try {
        await documentApi.upload({ knowledgeBaseId: this.knowledgeBaseId, file })
        await this.refresh()
        return true
      } catch (e) {
        this.error = e?.response?.data?.error?.message || e?.message || '上传失败'
        return false
      } finally {
        this.uploading = false
      }
    },

    /** 有 Pending/Processing 文档时每 2s 轮询一次（P1 没有 SignalR 推送）。 */
    ensurePolling() {
      // 状态枚举落库为 int：0 Pending / 1 Processing / 2 Ready / 3 Failed
      const unfinished = this.documents.some((d) => d.status === 0 || d.status === 1)
      if (!unfinished || this.polling) return

      this.polling = true
      const timer = setInterval(async () => {
        await this.refresh()
        const stillUnfinished = this.documents.some((d) => d.status === 0 || d.status === 1)
        if (!stillUnfinished) {
          clearInterval(timer)
          this.polling = false
        }
      }, 2000)
    },
  },
})

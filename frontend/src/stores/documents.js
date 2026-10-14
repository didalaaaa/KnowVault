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

    /**
     * 重新拉取知识库列表；preferredId 仍在列表里时优先选中它。
     * @param {string} [preferredId] 期望选中的知识库 ID
     */
    async reloadKnowledgeBases(preferredId) {
      this.knowledgeBases = await knowledgeBaseApi.list()
      const exists = this.knowledgeBases.some((kb) => kb.id === preferredId)
      this.knowledgeBaseId = exists ? preferredId : this.knowledgeBases[0]?.id || ''
      await this.refresh()
    },

    /**
     * 新建知识库并选中它（错误向上抛，由界面弹提示）。
     * @param {{name: string, description?: string}} payload 名称与描述
     * @returns {Promise<object>} 新建的知识库
     */
    async createKnowledgeBase(payload) {
      const created = await knowledgeBaseApi.create(payload)
      await this.reloadKnowledgeBases(created.id)
      return created
    },

    /**
     * 修改知识库（错误向上抛，由界面弹提示）。
     * @param {{id: string, name: string, description?: string}} payload 知识库 ID 与新名称、描述
     * @returns {Promise<object>} 修改后的知识库
     */
    async updateKnowledgeBase(payload) {
      const updated = await knowledgeBaseApi.update(payload)
      await this.reloadKnowledgeBases(this.knowledgeBaseId)
      return updated
    },

    /**
     * 删除知识库（仅空库；错误向上抛，由界面弹提示）。
     * @param {string} id 知识库 ID
     */
    async removeKnowledgeBase(id) {
      await knowledgeBaseApi.remove(id)
      await this.reloadKnowledgeBases(this.knowledgeBaseId === id ? '' : this.knowledgeBaseId)
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

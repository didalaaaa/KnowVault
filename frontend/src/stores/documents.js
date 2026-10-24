import { defineStore } from 'pinia'
import { getToken } from '../api/http'
import { documentApi } from '../api/documents'
import { knowledgeBaseApi } from '../api/knowledgeBases'

// 轮询定时器不放进 state：只是副作用句柄，不需要响应式
let pollTimer = null

// 文档与知识库状态。轮询只在存在未完成文档时进行，避免空转。
export const useDocumentStore = defineStore('documents', {
  state: () => ({
    knowledgeBases: [],
    knowledgeBaseId: '',
    documents: [],
    uploading: false,
    polling: false,
    // 正在删除的文档 ID；非空时界面显示全局遮罩并禁止其它操作
    deletingId: '',
    error: '',
  }),

  actions: {
    /**
     * 拉取知识库列表（含「已就绪 N 篇」计数）与当前知识库的文档列表；首次调用会选中默认知识库（后端已把默认库排在最前）。
     * 计数由后端在知识库列表里算好，所以文档每次刷新都要一并带上，否则轮询期间一直是旧值。
     * @param {string} [preferredId] 期望选中的知识库 ID；缺省保持当前选中项
     */
    async refresh(preferredId) {
      // 退出登录后旧轮询可能还会走到这里：没有令牌就不发请求，避免把 401 写成界面错误
      if (!getToken()) return
      try {
        this.knowledgeBases = await knowledgeBaseApi.list()
        const target = preferredId ?? this.knowledgeBaseId
        const exists = this.knowledgeBases.some((kb) => kb.id === target)
        this.knowledgeBaseId = exists ? target : this.knowledgeBases[0]?.id || ''
        this.documents = this.knowledgeBaseId ? await documentApi.list(this.knowledgeBaseId) : []
        this.error = ''
        this.ensurePolling()
      } catch (e) {
        this.error = e?.response?.data?.error?.message || e?.message || '加载知识库失败'
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
     * 新建知识库并选中它（错误向上抛，由界面弹提示）。
     * @param {{name: string, description?: string}} payload 名称与描述
     * @returns {Promise<object>} 新建的知识库
     */
    async createKnowledgeBase(payload) {
      const created = await knowledgeBaseApi.create(payload)
      await this.refresh(created.id)
      return created
    },

    /**
     * 修改知识库（错误向上抛，由界面弹提示）。
     * @param {{id: string, name: string, description?: string}} payload 知识库 ID 与新名称、描述
     * @returns {Promise<object>} 修改后的知识库
     */
    async updateKnowledgeBase(payload) {
      const updated = await knowledgeBaseApi.update(payload)
      await this.refresh()
      return updated
    },

    /**
     * 删除知识库（仅空库；错误向上抛，由界面弹提示）。
     * @param {string} id 知识库 ID
     */
    async removeKnowledgeBase(id) {
      await knowledgeBaseApi.remove(id)
      await this.refresh()
    },

    /**
     * 删除文档（错误向上抛，由界面弹提示）；整个过程中 deletingId 非空，界面据此加全局遮罩
     * @param {string} id 文档 ID
     */
    async removeDocument(id) {
      this.deletingId = id
      try {
        await documentApi.remove(id)
        await this.refresh()
      } finally {
        this.deletingId = ''
      }
    },

    /**
     * 重新处理文档（错误向上抛，由界面弹提示）。
     * @param {string} id 文档 ID
     */
    async reprocessDocument(id) {
      await documentApi.reprocess(id)
      await this.refresh()
    },

    /** 有 Pending/Processing 文档时每 2s 轮询一次（P1 没有 SignalR 推送）。 */
    ensurePolling() {
      // 状态枚举落库为 int：0 Pending / 1 Processing / 2 Ready / 3 Failed
      const unfinished = this.documents.some((d) => d.status === 0 || d.status === 1)
      if (!unfinished || this.polling) return

      this.polling = true
      pollTimer = setInterval(async () => {
        await this.refresh()
        const stillUnfinished = this.documents.some((d) => d.status === 0 || d.status === 1)
        if (!stillUnfinished) this.stopPolling()
      }, 2000)
    },

    /** 停止轮询；退出登录时必须调用，否则定时器会带着旧会话的请求干扰下一次登录。 */
    stopPolling() {
      if (pollTimer) clearInterval(pollTimer)
      pollTimer = null
      this.polling = false
    },
  },
})

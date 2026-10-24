import { defineStore } from 'pinia'
import { getToken } from '../api/http'
import { conversationApi } from '../api/conversations'

// 会话列表状态：只保存摘要，消息按需通过 chat store 拉取
export const useConversationStore = defineStore('conversations', {
  state: () => ({
    items: [],
    loading: false,
    error: '',
  }),

  actions: {
    /**
     * 拉取当前用户的会话列表。
     * @param {string} [knowledgeBaseId] 按知识库过滤；缺省取全部
     */
    async refresh(knowledgeBaseId) {
      if (!getToken()) return
      this.loading = true
      try {
        this.items = await conversationApi.list(knowledgeBaseId)
        this.error = ''
      } catch (e) {
        this.error = e?.response?.data?.error?.message || e?.message || '加载会话失败'
      } finally {
        this.loading = false
      }
    },

    /**
     * 重命名会话并同步列表。
     * @param {string} id 会话 ID
     * @param {string} title 新标题
     * @returns {Promise<object>} 更新后的会话摘要
     */
    async rename(id, title) {
      const updated = await conversationApi.rename({ id, title })
      const index = this.items.findIndex((item) => item.id === id)
      if (index >= 0) this.items[index] = { ...this.items[index], ...updated }
      return updated
    },

    /**
     * 删除会话并从列表移除。
     * @param {string} id 会话 ID
     */
    async remove(id) {
      await conversationApi.remove(id)
      this.items = this.items.filter((item) => item.id !== id)
    },

    /** 退出登录时清空列表。 */
    reset() {
      this.items = []
      this.error = ''
    },
  },
})

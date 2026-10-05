import { defineStore } from 'pinia'
import { qaApi } from '../api/qa'

// 聊天状态（P1：单会话连续问答，历史加载留 P2）
export const useChatStore = defineStore('chat', {
  state: () => ({
    connected: false,
    checking: false,
    loading: false,
    conversationId: null,
    messages: [], // { role: 'user' | 'assistant', content, citations?: [] }
  }),

  actions: {
    async checkConnection() {
      this.checking = true
      try {
        await qaApi.appConfig()
        this.connected = true
      } catch (e) {
        this.connected = false
      } finally {
        this.checking = false
      }
    },

    /**
     * 提问：带上知识库与会话，回答与引用一起渲染。
     * @param {string} question 用户问题
     * @param {string} knowledgeBaseId 目标知识库
     */
    async ask(question, knowledgeBaseId) {
      this.loading = true
      this.messages.push({ role: 'user', content: question })
      try {
        const res = await qaApi.ask({
          question,
          knowledgeBaseId: knowledgeBaseId || undefined,
          conversationId: this.conversationId || undefined,
        })
        // 记住会话：P1 不做历史加载，但同一会话的消息要连续落库
        this.conversationId = res.conversationId
        this.messages.push({
          role: 'assistant',
          content: res.answer,
          citations: res.citations || [],
        })
      } catch (e) {
        this.messages.push({
          role: 'assistant',
          content: '请求失败：' + (e?.response?.data?.error?.message || e?.message || e),
        })
      } finally {
        this.loading = false
      }
    },

    /** 清空当前会话（新会话）。 */
    reset() {
      this.messages = []
      this.conversationId = null
    },
  },
})

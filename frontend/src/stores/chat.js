import { defineStore } from 'pinia'
import { qaApi } from '../api/qa'

// 聊天状态（P0 骨架）
export const useChatStore = defineStore('chat', {
  state: () => ({
    connected: false,
    checking: false,
    loading: false,
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

    async ask(question) {
      this.loading = true
      this.messages.push({ role: 'user', content: question })
      try {
        const res = await qaApi.ask({ question })
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
  },
})

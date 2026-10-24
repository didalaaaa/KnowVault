import { defineStore } from 'pinia'
import { qaApi } from '../api/qa'
import { askStream, startQaHub } from '../api/qaHub'
import { conversationApi } from '../api/conversations'
import { feedbackApi } from '../api/feedback'

// 服务端流订阅句柄与收尾回调：放模块级而不是 state，避免把不可序列化的对象塞进 devtools 快照
let subscription = null
let finishStream = null

// 与 QaStreamEventKindEnum 保持一致（src/KnowVault.Application.Contracts/Models/Qa/QaStreamEventKindEnum.cs）
const EVENT_STARTED = 0
const EVENT_DELTA = 1
const EVENT_COMPLETED = 2

// 聊天状态（P2：SignalR 可用时逐字流式，否则降级一次性回答）
export const useChatStore = defineStore('chat', {
  state: () => ({
    connected: false,
    checking: false,
    loading: false,
    streaming: false,
    conversationId: null,
    // { id, role: 'user' | 'assistant', content, citations?, streaming?, feedback, latencyMs }
    messages: [],
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
     * 提问：优先走 SignalR 服务端流，连接不可用时降级为 REST 一次性回答。
     * @param {string} question 用户问题
     * @param {string} knowledgeBaseId 目标知识库
     * @param {boolean} [regenerate] 是否为重新生成（不追加用户气泡，服务端不重复落用户消息）
     */
    async ask(question, knowledgeBaseId, regenerate = false) {
      this.loading = true
      if (!regenerate) {
        this.messages.push({ id: null, role: 'user', content: question, feedback: null, latencyMs: null })
      }
      this.messages.push({
        id: null,
        role: 'assistant',
        content: '',
        citations: [],
        streaming: true,
        feedback: null,
        latencyMs: null,
      })
      // 必须从 state 里取回响应式引用：直接改 push 进去的原始对象不会触发视图更新
      const assistant = this.messages[this.messages.length - 1]

      const payload = {
        question,
        knowledgeBaseId: knowledgeBaseId || undefined,
        conversationId: this.conversationId || undefined,
        regenerate,
      }

      const hubReady = await startQaHub()
      if (!hubReady) {
        await this.askViaRest(payload, assistant)
        return
      }

      this.streaming = true
      try {
        await new Promise((resolve) => {
          finishStream = resolve
          subscription = askStream(payload, {
            onEvent: (event) => {
              if (event.kind === EVENT_STARTED) {
                this.conversationId = event.conversationId
                assistant.citations = event.citations || []
                // 首帧带回本轮用户消息 ID；重新生成时为空
                if (event.userMessageId) {
                  const user = [...this.messages].reverse().find((m) => m.role === 'user')
                  if (user) user.id = event.userMessageId
                }
                return
              }
              if (event.kind === EVENT_DELTA) {
                assistant.content += event.delta ?? ''
                return
              }
              if (event.kind === EVENT_COMPLETED) {
                assistant.id = event.assistantMessageId || null
                assistant.latencyMs = event.latencyMs ?? null
              }
            },
            onError: (err) => {
              assistant.content += (assistant.content ? '\n' : '') + '生成中断：' + describeError(err)
              resolve()
            },
            onComplete: () => resolve(),
          })
        })
      } catch (e) {
        assistant.content = '请求失败：' + describeError(e)
      } finally {
        subscription = null
        finishStream = null
        this.streaming = false
        assistant.streaming = false
        this.loading = false
        // 点「停止生成」时可能一个字都没收到，给个可见反馈，避免气泡空着
        if (!assistant.content) assistant.content = '（已停止生成）'
      }
    },

    /**
     * 重新生成最后一条回答：移除旧气泡，带同一条问题重发，服务端会剔除上一轮上下文。
     * @param {string} knowledgeBaseId 目标知识库
     */
    async regenerate(knowledgeBaseId) {
      if (this.loading) return
      const lastUser = [...this.messages].reverse().find((m) => m.role === 'user')
      if (!lastUser) return
      if (this.messages[this.messages.length - 1]?.role === 'assistant') this.messages.pop()
      await this.ask(lastUser.content, knowledgeBaseId, true)
    },

    /**
     * 点赞/点踩；再次点击同一评分表示取消。先改本地再发请求，失败回滚。
     * @param {number} index 消息下标
     * @param {number} rating 0 点赞 / 1 点踩
     */
    async feedback(index, rating) {
      const message = this.messages[index]
      if (!message?.id || message.role !== 'assistant') return

      const previous = message.feedback
      const next = previous === rating ? null : rating
      message.feedback = next

      try {
        const saved = await feedbackApi.set({ messageId: message.id, rating: next })
        message.feedback = saved ?? null
      } catch (e) {
        message.feedback = previous
        throw e
      }
    },

    /**
     * 复制回答正文到剪贴板。
     * @param {number} index 消息下标
     * @returns {Promise<boolean>} 是否复制成功
     */
    async copy(index) {
      const message = this.messages[index]
      if (!message?.content) return false
      try {
        await navigator.clipboard.writeText(message.content)
        return true
      } catch (e) {
        return false
      }
    },

    /**
     * 停止生成：取消服务端流。客户端断开后服务端迭代器随即退出，不会落半截助手消息。
     */
    stop() {
      subscription?.dispose()
      subscription = null
      this.streaming = false
      const last = this.messages[this.messages.length - 1]
      if (last && last.role === 'assistant') last.streaming = false
      finishStream?.()
    },

    /**
     * REST 降级路径：与 P1 完全一致的一次性回答。
     * @param {object} payload 提问参数
     * @param {object} assistant state 里的助手消息（响应式引用）
     */
    async askViaRest(payload, assistant) {
      try {
        const res = await qaApi.ask(payload)
        this.conversationId = res.conversationId
        assistant.content = res.answer
        assistant.citations = res.citations || []
      } catch (e) {
        assistant.content = '请求失败：' + describeError(e)
      } finally {
        assistant.streaming = false
        this.loading = false
      }
    },

    /**
     * 载入历史会话：停止当前流后整体替换消息与引用。
     * @param {string} id 会话 ID
     */
    async loadConversation(id) {
      this.stop()
      this.loading = true
      try {
        const messages = await conversationApi.messages(id)
        this.messages = messages.map((m) => ({
          id: m.id,
          // 与 MessageRoleEnum 一致：0 用户 / 1 助手
          role: m.role === 0 ? 'user' : 'assistant',
          content: m.content,
          citations: m.citations || [],
          streaming: false,
          feedback: m.feedback ?? null,
          latencyMs: m.latencyMs ?? null,
        }))
        this.conversationId = id
      } catch (e) {
        this.messages = []
        this.conversationId = null
        throw e
      } finally {
        this.loading = false
      }
    },

    /** 清空当前会话（新会话）。 */
    reset() {
      this.stop()
      this.messages = []
      this.conversationId = null
    },
  },
})

/**
 * 把未知错误整理成一行可读文案。
 * @param {any} err 错误对象
 * @returns {string} 错误文案
 */
function describeError(err) {
  return err?.response?.data?.error?.message || err?.message || String(err)
}

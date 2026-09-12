import { http } from './http'

// 后端 API 封装（P0 骨架：问答 + 连接探测）
export const qaApi = {
  // POST /api/app/qa/ask -> { conversationId, answer, citations[] }
  ask: (payload) => http.post('/app/qa/ask', payload),

  // 连接探测：ABP 内置应用配置端点
  appConfig: () => http.get('/abp/application-configuration'),
}

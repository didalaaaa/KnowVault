import { HubConnectionBuilder, HubConnectionState, LogLevel } from '@microsoft/signalr'
import { getToken } from './http'

// 与 QaHub.Route 保持一致（src/KnowVault.HttpApi/Hubs/QaHub.cs）
const HUB_URL = '/signalr/qa'

// 单例连接：SignalR 连接本身可复用，每次提问只新建一条 stream
let connection = null

/**
 * 取（或创建）Hub 连接。
 * @returns {object} HubConnection 实例
 */
function getConnection() {
  if (connection) return connection
  connection = new HubConnectionBuilder()
    .withUrl(HUB_URL, { accessTokenFactory: () => getToken() ?? '' })
    .withAutomaticReconnect()
    .configureLogging(LogLevel.Warning)
    .build()
  return connection
}

/**
 * 建立连接（已连接时直接返回 true）。
 * @returns {Promise<boolean>} 连接是否可用；false 表示调用方应降级为 REST
 */
export async function startQaHub() {
  const conn = getConnection()
  if (conn.state === HubConnectionState.Connected) return true
  try {
    await conn.start()
    return true
  } catch (e) {
    // 后端没起、令牌无效都会走到这里：降级为一次性回答，不让界面卡住
    console.warn('[KnowVault] SignalR 连接失败，降级为一次性回答', e)
    return false
  }
}

/**
 * 以服务端流的方式提问。
 * @param {object} input 提问参数（与 REST 同一个 DTO）
 * @param {{onEvent: Function, onError: Function, onComplete: Function}} handlers 事件回调
 * @returns {{dispose: Function}} 订阅句柄，dispose() 即取消生成
 */
export function askStream(input, { onEvent, onError, onComplete }) {
  return getConnection()
    .stream('Ask', input)
    .subscribe({ next: onEvent, error: onError, complete: onComplete })
}

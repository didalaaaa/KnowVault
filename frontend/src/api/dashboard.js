import { http } from './http'

// 仪表盘接口（概览页统计）
export const dashboardApi = {
  /**
   * 取当前用户可见范围的概览统计。
   * @returns {Promise<object>} 汇总数据与 7 天趋势
   */
  get: () => http.get('/app/dashboard'),
}

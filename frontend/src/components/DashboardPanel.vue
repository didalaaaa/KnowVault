<template>
  <div class="dash-panel">
    <div class="dash-header">
      <div>
        <span class="dash-eyebrow">Overview</span>
        <h2 class="dash-title">运行概览</h2>
        <p class="dash-subtitle">你的资料规模与问答使用情况</p>
      </div>
      <n-button size="small" quaternary :loading="loading" @click="load">
        <template #icon><KvIcon name="refresh" :size="14" /></template>
        刷新
      </n-button>
    </div>

    <n-alert v-if="error" type="error" closable @close="error = ''">
      {{ error }}
      <n-button text type="primary" size="tiny" @click="load">重试</n-button>
    </n-alert>

    <div v-if="loading && !data" class="stat-grid" aria-busy="true" aria-label="正在加载概览">
      <div v-for="n in 4" :key="n" class="stat-card stat-card-skeleton">
        <n-skeleton circle :width="38" :height="38" style="flex: none" />
        <div class="stat-body">
          <n-skeleton text style="width: 56%" />
          <n-skeleton text :repeat="2" style="margin-top: 8px" />
        </div>
      </div>
    </div>

    <template v-else-if="data">
      <div class="stat-grid">
        <article v-for="card in cards" :key="card.key" class="stat-card">
          <span class="stat-icon"><KvIcon :name="card.icon" :size="18" :stroke-width="1.7" /></span>
          <div class="stat-body">
            <span class="stat-label">{{ card.label }}</span>
            <strong class="stat-value">
              {{ card.value }}<small v-if="card.unit">{{ card.unit }}</small>
            </strong>
            <span class="stat-hint">{{ card.hint }}</span>
          </div>
        </article>
      </div>

      <n-alert v-if="data.failedDocumentCount > 0" type="warning" :show-icon="false" class="dash-alert">
        有 {{ data.failedDocumentCount }} 篇文档处理失败
        <n-button text type="primary" size="tiny" @click="$emit('open-knowledge')">去知识库查看</n-button>
      </n-alert>

      <section class="chart-card">
        <div class="chart-head">
          <div>
            <h3>最近 7 天提问</h3>
            <p>每天基于知识库发起的提问数</p>
          </div>
          <span class="chart-total">7 天 {{ trendTotal }} 次</span>
        </div>
        <div ref="chartEl" class="chart-canvas" role="img" :aria-label="chartSummary"></div>
        <p v-if="trendEmpty" class="chart-empty">最近 7 天还没有提问，去问答页试试</p>
      </section>

      <section v-if="usage" class="usage-section">
        <div class="usage-head">
          <div>
            <h3>用量与成本</h3>
            <p>最近 7 天模型调用的 token 与估算费用</p>
          </div>
          <div class="usage-badges">
            <span v-if="usage.hasEstimated" class="usage-tag">含本地估算</span>
            <span class="usage-note">按配置价估算，以账单为准</span>
          </div>
        </div>

        <div class="usage-grid">
          <article v-for="card in usageCards" :key="card.key" class="usage-card">
            <span class="usage-label">{{ card.label }}</span>
            <strong class="usage-value">
              {{ card.value }}<small v-if="card.unit">{{ card.unit }}</small>
            </strong>
            <span class="usage-hint">{{ card.hint }}</span>
          </article>
        </div>

        <div class="chart-card">
          <div class="chart-head">
            <div>
              <h3>7 天 token 用量</h3>
              <p>按调用日期汇总，悬停查看输入、输出与成本</p>
            </div>
            <span class="chart-total">7 天 {{ formatTokens(usage.last7DayTokens) }} tokens</span>
          </div>
          <div ref="usageChartEl" class="chart-canvas" role="img" aria-label="最近 7 天 token 用量"></div>
          <p v-if="usageEmpty" class="chart-empty">还没有用量记录，提问或上传文档后会出现在这里</p>
        </div>

        <div class="usage-table-card">
          <div class="chart-head">
            <div>
              <h3>按知识库</h3>
              <p>多库/全局问答单列；已删除的库显示为占位名</p>
            </div>
          </div>
          <table class="usage-table">
            <thead>
              <tr>
                <th>知识库</th>
                <th class="num">提问</th>
                <th class="num">Tokens</th>
                <th class="num">估算成本</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="row in usage.byKnowledgeBase" :key="row.knowledgeBaseId || 'global'">
                <td>{{ row.knowledgeBaseName }}</td>
                <td class="num">{{ row.questionCount }}</td>
                <td class="num">{{ formatTokens(row.totalTokens) }}</td>
                <td class="num">{{ formatCost(row.estimatedCostCny) }}</td>
              </tr>
              <tr v-if="!usage.byKnowledgeBase.length">
                <td colspan="4" class="usage-empty">暂无按库用量</td>
              </tr>
            </tbody>
          </table>
        </div>
      </section>
    </template>
  </div>
</template>

<script setup>
import { computed, nextTick, onBeforeUnmount, onMounted, ref } from 'vue'
import { NAlert, NButton, NSkeleton } from 'naive-ui'
import * as echarts from 'echarts/core'
import { BarChart, LineChart } from 'echarts/charts'
import { GridComponent, TooltipComponent } from 'echarts/components'
import { CanvasRenderer } from 'echarts/renderers'
import KvIcon from './KvIcon.vue'
import { dashboardApi } from '../api/dashboard'

echarts.use([BarChart, LineChart, GridComponent, TooltipComponent, CanvasRenderer])

defineEmits(['open-knowledge'])

const data = ref(null)
const error = ref('')
const loading = ref(false)
const chartEl = ref(null)
const usageChartEl = ref(null)
let chart = null
let usageChart = null

/** 四张统计卡；耗时没有样本时显示「—」而不是 0。 */
const cards = computed(() => {
  const d = data.value
  if (!d) return []
  return [
    { key: 'docs', icon: 'file-text', label: '文档总数', value: d.documentCount, hint: `${d.knowledgeBaseCount} 个知识库` },
    {
      key: 'ready',
      icon: 'shield',
      label: '已就绪',
      value: d.readyDocumentCount,
      hint: d.failedDocumentCount > 0 ? `${d.failedDocumentCount} 篇处理失败` : '全部解析完成',
    },
    { key: 'questions', icon: 'quote', label: '累计提问', value: d.questionCount, hint: `今日 ${d.todayQuestionCount} 次` },
    {
      key: 'latency',
      icon: 'sparkles',
      label: '平均耗时',
      value: d.averageLatencyMs > 0 ? (d.averageLatencyMs / 1000).toFixed(1) : '—',
      unit: d.averageLatencyMs > 0 ? 's' : '',
      hint: '最近 7 天助手回答',
    },
  ]
})

const trendTotal = computed(() =>
  (data.value?.last7DayQuestions || []).reduce((sum, point) => sum + point.count, 0),
)

const trendEmpty = computed(() => trendTotal.value === 0)

const chartSummary = computed(() => `最近 7 天共提问 ${trendTotal.value} 次`)

/** 用量区数据；后端始终返回，空账号时各值为 0。 */
const usage = computed(() => data.value?.usage || null)

/** 用量区四张卡。 */
const usageCards = computed(() => {
  const u = usage.value
  if (!u) return []
  return [
    {
      key: 'today-tokens',
      label: '今日 tokens',
      value: formatTokens(u.todayTokens),
      hint: '生成 + 检索 + 摄取',
    },
    {
      key: 'today-cost',
      label: '今日估算成本',
      value: formatCost(u.todayEstimatedCostCny),
      hint: u.hasEstimated ? '含本地估算' : '按配置价估算',
    },
    {
      key: 'week-tokens',
      label: '7 天 tokens',
      value: formatTokens(u.last7DayTokens),
      hint: '最近 7 天合计',
    },
    {
      key: 'avg-tokens',
      label: '平均每次提问',
      value: formatTokens(u.averageTokensPerQuestion),
      unit: 'tokens',
      hint: '按问答链路合计',
    },
  ]
})

const usageEmpty = computed(() => (usage.value?.last7DayTokens || 0) === 0)

/**
 * 千分位格式化 token 数。
 * @param {number} value token 数
 * @returns {string} 格式化文本
 */
function formatTokens(value) {
  return Number(value || 0).toLocaleString('zh-CN')
}

/**
 * 估算成本：零显示 ¥0，小额保留四位小数。
 * @param {number} value 人民币元
 * @returns {string} 格式化文本
 */
function formatCost(value) {
  const amount = Number(value || 0)
  if (amount === 0) return '¥0'
  return `¥${amount < 0.01 ? amount.toFixed(4) : amount.toFixed(2)}`
}

/**
 * 把后端日期格式化成 MM-DD。
 * @param {string} value ISO 日期
 * @returns {string} 月-日
 */
function formatDate(value) {
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return ''
  return `${String(date.getMonth() + 1).padStart(2, '0')}-${String(date.getDate()).padStart(2, '0')}`
}

/** 拉取概览数据；首次加载显示骨架，刷新时保留旧数据。 */
async function load() {
  loading.value = true
  try {
    data.value = await dashboardApi.get()
    error.value = ''
    await nextTick()
    renderChart()
    renderUsageChart()
  } catch (e) {
    error.value = e?.response?.data?.error?.message || e?.message || '加载概览失败'
  } finally {
    loading.value = false
  }
}

/** 渲染/刷新 7 天趋势折线。 */
function renderChart() {
  if (!chartEl.value || !data.value) return
  if (!chart) chart = echarts.init(chartEl.value)

  const points = data.value.last7DayQuestions || []
  chart.setOption({
    grid: { left: 8, right: 14, top: 18, bottom: 6, containLabel: true },
    tooltip: {
      trigger: 'axis',
      backgroundColor: '#ffffff',
      borderColor: '#e4e7df',
      textStyle: { color: '#17221d', fontSize: 12 },
      formatter: (params) => `${params[0].axisValue}<br/>提问 ${params[0].value} 次`,
    },
    xAxis: {
      type: 'category',
      boundaryGap: false,
      data: points.map((point) => formatDate(point.date)),
      axisLine: { lineStyle: { color: '#e4e7df' } },
      axisTick: { show: false },
      axisLabel: { color: '#6b7670', fontSize: 11 },
    },
    yAxis: {
      type: 'value',
      minInterval: 1,
      splitLine: { lineStyle: { color: '#eef0ea' } },
      axisLabel: { color: '#6b7670', fontSize: 11 },
    },
    series: [
      {
        type: 'line',
        data: points.map((point) => point.count),
        smooth: true,
        symbol: 'circle',
        symbolSize: 6,
        lineStyle: { width: 2.5, color: '#0f766e' },
        itemStyle: { color: '#0f766e', borderColor: '#ffffff', borderWidth: 2 },
        areaStyle: { color: 'rgba(15, 118, 110, 0.10)' },
      },
    ],
  }, true)
}

/** 渲染/刷新 7 天 token 柱状图。 */
function renderUsageChart() {
  if (!usageChartEl.value || !usage.value) return
  if (!usageChart) usageChart = echarts.init(usageChartEl.value)

  const points = usage.value.last7DayTrend || []
  usageChart.setOption({
    grid: { left: 8, right: 14, top: 18, bottom: 6, containLabel: true },
    tooltip: {
      trigger: 'axis',
      axisPointer: { type: 'shadow' },
      backgroundColor: '#ffffff',
      borderColor: '#e4e7df',
      textStyle: { color: '#17221d', fontSize: 12 },
      formatter: (params) => {
        const index = params[0]?.dataIndex ?? 0
        const point = points[index] || {}
        return [
          params[0]?.axisValue,
          `输入 ${formatTokens(point.promptTokens)}`,
          `输出 ${formatTokens(point.completionTokens)}`,
          `合计 ${formatTokens(point.totalTokens)}`,
          `成本 ${formatCost(point.estimatedCostCny)}`,
        ].join('<br/>')
      },
    },
    xAxis: {
      type: 'category',
      data: points.map((point) => formatDate(point.date)),
      axisLine: { lineStyle: { color: '#e4e7df' } },
      axisTick: { show: false },
      axisLabel: { color: '#6b7670', fontSize: 11 },
    },
    yAxis: {
      type: 'value',
      minInterval: 1,
      splitLine: { lineStyle: { color: '#eef0ea' } },
      axisLabel: { color: '#6b7670', fontSize: 11 },
    },
    series: [
      {
        type: 'bar',
        data: points.map((point) => point.totalTokens),
        barMaxWidth: 32,
        itemStyle: { color: '#0f766e', borderRadius: [6, 6, 0, 0] },
      },
    ],
  }, true)
}

/** 容器宽度变化时跟随重绘。 */
function onResize() {
  chart?.resize()
  usageChart?.resize()
}

onMounted(() => {
  load()
  window.addEventListener('resize', onResize)
})

onBeforeUnmount(() => {
  window.removeEventListener('resize', onResize)
  chart?.dispose()
  chart = null
  usageChart?.dispose()
  usageChart = null
})

defineExpose({ refresh: load })
</script>

<style scoped>
.dash-panel {
  max-width: 1080px;
  margin: 0 auto;
  display: flex;
  flex-direction: column;
  gap: 18px;
  padding-bottom: 12px;
}

.dash-header {
  display: flex;
  align-items: flex-end;
  justify-content: space-between;
  gap: 16px;
}

.dash-eyebrow {
  font-size: 10.5px;
  font-weight: 700;
  letter-spacing: 0.16em;
  text-transform: uppercase;
  color: var(--kv-primary);
}

.dash-title {
  margin: 4px 0 0;
  font-size: 24px;
  font-weight: 700;
  letter-spacing: -0.02em;
  color: var(--kv-text);
}

.dash-subtitle {
  margin: 6px 0 0;
  font-size: 13px;
  color: var(--kv-text-3);
}

.stat-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(210px, 1fr));
  gap: 14px;
}

.stat-card {
  display: flex;
  align-items: flex-start;
  gap: 14px;
  padding: 18px;
  border: 1px solid var(--kv-border);
  border-radius: 16px;
  background: var(--kv-surface);
  box-shadow: var(--kv-shadow-sm);
  transition: transform 0.2s ease, box-shadow 0.2s ease, border-color 0.2s ease;
}

.stat-card:hover {
  transform: translateY(-2px);
  border-color: var(--kv-border-strong);
  box-shadow: var(--kv-shadow-md);
}

.stat-card-skeleton {
  pointer-events: none;
}

.stat-icon {
  width: 38px;
  height: 38px;
  flex: none;
  display: grid;
  place-items: center;
  border: 1px solid #d9eae5;
  border-radius: 11px;
  color: var(--kv-primary);
  background: var(--kv-primary-soft);
}

.stat-body {
  min-width: 0;
  display: flex;
  flex-direction: column;
  gap: 2px;
}

.stat-label {
  font-size: 12px;
  font-weight: 600;
  color: var(--kv-text-3);
}

.stat-value {
  font-size: 26px;
  font-weight: 700;
  line-height: 1.25;
  letter-spacing: -0.02em;
  color: var(--kv-text);
  font-variant-numeric: tabular-nums;
}

.stat-value small {
  margin-left: 2px;
  font-size: 13px;
  font-weight: 600;
  color: var(--kv-text-3);
}

.stat-hint {
  font-size: 11.5px;
  color: var(--kv-text-3);
}

.dash-alert {
  border-radius: 12px;
}

.chart-card {
  padding: 20px;
  border: 1px solid var(--kv-border);
  border-radius: 16px;
  background: var(--kv-surface);
  box-shadow: var(--kv-shadow-sm);
}

.chart-head {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 12px;
  margin-bottom: 8px;
}

.chart-head h3 {
  margin: 0;
  font-size: 15px;
  font-weight: 650;
  letter-spacing: -0.01em;
  color: var(--kv-text);
}

.chart-head p {
  margin: 4px 0 0;
  font-size: 12px;
  color: var(--kv-text-3);
}

.chart-total {
  flex: none;
  padding: 4px 10px;
  border-radius: 999px;
  background: var(--kv-primary-soft);
  color: var(--kv-primary);
  font-size: 11.5px;
  font-weight: 600;
}

.chart-canvas {
  width: 100%;
  height: 240px;
}

.chart-empty {
  margin: 4px 0 0;
  text-align: center;
  font-size: 12px;
  color: var(--kv-text-3);
}

.usage-section {
  display: flex;
  flex-direction: column;
  gap: 14px;
}

.usage-head {
  display: flex;
  align-items: flex-end;
  justify-content: space-between;
  gap: 12px;
  padding-top: 4px;
}

.usage-head h3 {
  margin: 0;
  font-size: 17px;
  font-weight: 650;
  letter-spacing: -0.01em;
  color: var(--kv-text);
}

.usage-head p {
  margin: 4px 0 0;
  font-size: 12px;
  color: var(--kv-text-3);
}

.usage-badges {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 8px;
}

.usage-tag {
  padding: 3px 9px;
  border-radius: 999px;
  background: #fef3c7;
  color: #92400e;
  font-size: 11px;
  font-weight: 600;
}

.usage-note {
  font-size: 11.5px;
  color: var(--kv-text-3);
}

.usage-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(180px, 1fr));
  gap: 12px;
}

.usage-card {
  display: flex;
  flex-direction: column;
  gap: 4px;
  padding: 16px 18px;
  border: 1px solid var(--kv-border);
  border-radius: 14px;
  background: var(--kv-surface);
  box-shadow: var(--kv-shadow-sm);
}

.usage-label {
  font-size: 12px;
  font-weight: 600;
  color: var(--kv-text-3);
}

.usage-value {
  font-size: 24px;
  font-weight: 700;
  line-height: 1.25;
  letter-spacing: -0.02em;
  color: var(--kv-text);
  font-variant-numeric: tabular-nums;
}

.usage-value small {
  margin-left: 2px;
  font-size: 12px;
  font-weight: 600;
  color: var(--kv-text-3);
}

.usage-hint {
  font-size: 11.5px;
  color: var(--kv-text-3);
}

.usage-table-card {
  padding: 20px;
  border: 1px solid var(--kv-border);
  border-radius: 16px;
  background: var(--kv-surface);
  box-shadow: var(--kv-shadow-sm);
}

.usage-table {
  width: 100%;
  border-collapse: collapse;
  font-size: 12.5px;
}

.usage-table th {
  padding: 8px 10px;
  text-align: left;
  font-weight: 600;
  color: var(--kv-text-3);
  border-bottom: 1px solid var(--kv-border);
}

.usage-table td {
  padding: 10px;
  color: var(--kv-text);
  border-bottom: 1px solid var(--kv-border);
}

.usage-table tr:last-child td {
  border-bottom: none;
}

.usage-table .num {
  text-align: right;
  font-variant-numeric: tabular-nums;
}

.usage-empty {
  padding: 16px 0;
  text-align: center;
  color: var(--kv-text-3);
}

@media (max-width: 640px) {
  .dash-title {
    font-size: 21px;
  }

  .chart-canvas {
    height: 200px;
  }
}
</style>

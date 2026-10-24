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
        <n-button text type="primary" size="tiny" @click="$emit('open-qa')">去资料库查看</n-button>
      </n-alert>

      <section class="chart-card">
        <div class="chart-head">
          <div>
            <h3>最近 7 天提问</h3>
            <p>每天基于资料库发起的提问数</p>
          </div>
          <span class="chart-total">7 天 {{ trendTotal }} 次</span>
        </div>
        <div ref="chartEl" class="chart-canvas" role="img" :aria-label="chartSummary"></div>
        <p v-if="trendEmpty" class="chart-empty">最近 7 天还没有提问，去问答页试试</p>
      </section>
    </template>
  </div>
</template>

<script setup>
import { computed, nextTick, onBeforeUnmount, onMounted, ref } from 'vue'
import { NAlert, NButton, NSkeleton } from 'naive-ui'
import * as echarts from 'echarts/core'
import { LineChart } from 'echarts/charts'
import { GridComponent, TooltipComponent } from 'echarts/components'
import { CanvasRenderer } from 'echarts/renderers'
import KvIcon from './KvIcon.vue'
import { dashboardApi } from '../api/dashboard'

echarts.use([LineChart, GridComponent, TooltipComponent, CanvasRenderer])

defineEmits(['open-qa'])

const data = ref(null)
const error = ref('')
const loading = ref(false)
const chartEl = ref(null)
let chart = null

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

/** 容器宽度变化时跟随重绘。 */
function onResize() {
  chart?.resize()
}

onMounted(() => {
  load()
  window.addEventListener('resize', onResize)
})

onBeforeUnmount(() => {
  window.removeEventListener('resize', onResize)
  chart?.dispose()
  chart = null
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

@media (max-width: 640px) {
  .dash-title {
    font-size: 21px;
  }

  .chart-canvas {
    height: 200px;
  }
}
</style>

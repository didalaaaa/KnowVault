<template>
  <n-drawer :show="show" :width="drawerWidth" placement="right" @update:show="(value) => emit('update:show', value)">
    <n-drawer-content :native-scrollbar="false" closable>
      <template #header>
        <div class="drawer-head">
          <span class="head-icon"><KvIcon name="quote" :size="15" /></span>
          <h2 class="drawer-title">引用详情</h2>
        </div>
      </template>

      <div v-if="citation" class="citation-body">
        <section class="source-card" aria-label="引用来源">
          <div class="source-title-row">
            <h3 class="source-title" :title="citation.title">{{ citation.title }}</h3>
            <span v-if="scoreText" class="score-pill">{{ scoreText }}</span>
          </div>
          <div class="source-meta">
            <span class="meta-chip">片段 #{{ citation.chunkIndex }}</span>
            <span v-if="headingPath" class="meta-chip heading" :title="headingPath">{{ headingPath }}</span>
          </div>
        </section>

        <section class="snippet-section" aria-label="命中片段">
          <div class="section-head">
            <h3>命中片段</h3>
            <span>{{ chunk ? '完整切片' : '摘要' }}</span>
          </div>

          <div v-if="loading" class="snippet-loading" aria-live="polite">
            <n-spin :size="18" />
            <span>正在读取切片…</span>
          </div>

          <p v-else class="snippet-text">{{ displayText }}</p>

          <p v-if="loadError" class="snippet-hint">{{ loadError }}</p>
        </section>
      </div>

      <template #footer>
        <div class="drawer-footer">
          <n-button type="primary" @click="emit('open-document', citation)">
            <template #icon><KvIcon name="file-text" :size="15" /></template>
            查看文档
          </n-button>
        </div>
      </template>
    </n-drawer-content>
  </n-drawer>
</template>

<script setup>
import { computed, ref, watch } from 'vue'
import { NButton, NDrawer, NDrawerContent, NSpin } from 'naive-ui'
import KvIcon from './KvIcon.vue'
import { documentApi } from '../api/documents'

const props = defineProps({
  show: { type: Boolean, default: false },
  citation: { type: Object, default: null },
})
const emit = defineEmits(['update:show', 'open-document'])

const loading = ref(false)
const chunk = ref(null)
const loadError = ref('')

const drawerWidth = 'min(560px, 100vw)'

// 旧会话的引用没有 headingPath：优先用切片全文里的标题路径，回落到引用自身携带的字段
const headingPath = computed(() => chunk.value?.headingPath || props.citation?.headingPath || '')
const displayText = computed(
  () => chunk.value?.content || props.citation?.snippet || '未能读取到片段正文，可打开文档详情查看',
)
const scoreText = computed(() =>
  typeof props.citation?.score === 'number' ? `相似度 ${props.citation.score.toFixed(3)}` : '',
)

watch(
  () => [props.show, props.citation?.documentId, props.citation?.chunkIndex],
  ([show]) => {
    if (show) loadChunk()
  },
)

/**
 * 拉取文档切片并定位命中片段；失败时退回引用自带的摘要
 */
async function loadChunk() {
  chunk.value = null
  loadError.value = ''
  if (!props.citation) return

  loading.value = true
  try {
    const chunks = await documentApi.getChunks(props.citation.documentId)
    chunk.value = chunks.find((item) => item.chunkIndex === props.citation.chunkIndex) || null
    if (!chunk.value) loadError.value = '未找到对应切片，当前展示引用摘要'
  } catch (e) {
    loadError.value = describeError(e)
  } finally {
    loading.value = false
  }
}

/**
 * 把未知错误整理成一行可读文案
 * @param {any} e 错误对象
 * @returns {string} 错误文案
 */
function describeError(e) {
  const message = e?.response?.data?.error?.message || e?.message
  return message ? `切片读取失败：${message}` : '切片读取失败，当前展示引用摘要'
}
</script>

<style scoped>
.drawer-head {
  display: flex;
  align-items: center;
  gap: 9px;
}

.head-icon {
  width: 26px;
  height: 26px;
  display: grid;
  place-items: center;
  border-radius: 8px;
  background: var(--kv-accent-soft);
  color: var(--kv-accent);
}

.drawer-title {
  margin: 0;
  font-size: 16px;
  font-weight: 650;
  letter-spacing: -0.01em;
  color: var(--kv-text);
}

.citation-body {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.source-card {
  display: flex;
  flex-direction: column;
  gap: 10px;
  padding: 14px 16px;
  border: 1px solid var(--kv-border);
  border-radius: var(--kv-radius-lg);
  background: var(--kv-surface-soft);
}

.source-title-row {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 12px;
}

.source-title {
  margin: 0;
  font-size: 15px;
  font-weight: 650;
  line-height: 1.5;
  color: var(--kv-text);
  overflow-wrap: anywhere;
}

.score-pill {
  flex: none;
  padding: 3px 9px;
  border-radius: 999px;
  background: var(--kv-primary-soft);
  color: var(--kv-primary);
  font-size: 11.5px;
  font-weight: 600;
  font-variant-numeric: tabular-nums;
}

.source-meta {
  display: flex;
  flex-wrap: wrap;
  gap: 6px;
}

.meta-chip {
  max-width: 100%;
  padding: 3px 9px;
  border: 1px solid var(--kv-border);
  border-radius: 6px;
  background: #ffffff;
  font-size: 11.5px;
  color: var(--kv-text-2);
}

.meta-chip.heading {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  color: var(--kv-primary);
  border-color: #d6e9e4;
}

.snippet-section {
  display: flex;
  flex-direction: column;
  gap: 10px;
}

.section-head {
  display: flex;
  align-items: baseline;
  justify-content: space-between;
  gap: 12px;
}

.section-head h3 {
  margin: 0;
  font-size: 13px;
  font-weight: 650;
  letter-spacing: 0.02em;
  color: var(--kv-text);
}

.section-head span {
  font-size: 11.5px;
  color: var(--kv-text-3);
}

.snippet-loading {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 18px 0;
  font-size: 13px;
  color: var(--kv-text-3);
}

.snippet-text {
  max-height: 52vh;
  overflow-y: auto;
  margin: 0;
  padding: 14px 16px;
  border: 1px solid var(--kv-border);
  border-radius: var(--kv-radius-lg);
  background: #fbfcf9;
  font-size: 14px;
  line-height: 1.8;
  color: var(--kv-text);
  white-space: pre-wrap;
  overflow-wrap: anywhere;
}

.snippet-hint {
  margin: 0;
  font-size: 12px;
  line-height: 1.6;
  color: var(--kv-warning, #b45309);
}

.drawer-footer {
  display: flex;
  justify-content: flex-end;
}

@media (max-width: 560px) {
  .source-title-row {
    flex-direction: column;
    gap: 8px;
  }

  .snippet-text {
    max-height: 46vh;
  }
}
</style>

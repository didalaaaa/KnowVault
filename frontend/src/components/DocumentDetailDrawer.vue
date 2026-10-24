<template>
  <n-drawer :show="show" :width="drawerWidth" placement="right" @update:show="(value) => emit('update:show', value)">
    <n-drawer-content :native-scrollbar="false" closable>
      <template #header>
        <div class="drawer-head">
          <span class="file-badge">{{ sourceLabel }}</span>
          <div class="drawer-title-wrap">
            <h2 class="drawer-title" :title="document?.title">{{ document?.title || '文档详情' }}</h2>
            <span v-if="document" class="status-pill" :class="'status-' + document.status">
              <span class="status-dot"></span>
              {{ statusText }}
            </span>
          </div>
        </div>
      </template>

      <div v-if="document" class="detail-body">
        <section class="meta-card" aria-label="文档信息">
          <div class="meta-item">
            <span class="meta-label">来源类型</span>
            <span class="meta-value">{{ sourceLabel }}</span>
          </div>
          <div class="meta-item">
            <span class="meta-label">上传时间</span>
            <span class="meta-value">{{ formatTime(document.creationTime) }}</span>
          </div>
          <div class="meta-item">
            <span class="meta-label">切片数量</span>
            <span class="meta-value">{{ chunks.length || '—' }}</span>
          </div>
          <div class="meta-item">
            <span class="meta-label">处理次数</span>
            <span class="meta-value">{{ document.ingestAttemptCount || 1 }}</span>
          </div>
        </section>

        <n-alert v-if="document.errorMessage" type="error" :show-icon="false" title="处理失败">
          {{ document.errorMessage }}
        </n-alert>

        <div class="detail-actions">
          <n-button type="primary" :loading="previewing" :disabled="!document.objectStorageKey" @click="preview">
            <template #icon><KvIcon :name="previewable ? 'search' : 'file-text'" :size="15" /></template>
            {{ previewable ? '预览原件' : '下载原件' }}
          </n-button>
          <n-button v-if="previewable" quaternary :loading="downloading" :disabled="!document.objectStorageKey" @click="download">
            <template #icon><KvIcon name="arrow-right" :size="15" /></template>
            下载
          </n-button>
        </div>

        <n-alert v-if="previewError" type="warning" :show-icon="false" closable @close="previewError = ''">
          {{ previewError }}
        </n-alert>

        <section v-if="previewMode" class="preview-card" aria-label="原件预览">
          <div class="preview-head">
            <span>{{ previewMode === 'text' ? 'Markdown 原文' : '原件预览' }}</span>
            <n-button size="tiny" quaternary @click="closePreview">关闭预览</n-button>
          </div>
          <pre v-if="previewMode === 'text'" class="preview-text">{{ previewText }}</pre>
          <!-- HTML 用空 sandbox 禁止脚本；PDF 需要浏览器内置阅读器，加了 sandbox 会白屏 -->
          <iframe
            v-else
            class="preview-frame"
            :src="previewUrl"
            :sandbox="document.sourceType === 4 ? '' : undefined"
            title="文档原件预览"
          ></iframe>
        </section>

        <section class="chunk-section" aria-label="文档切片">
          <div class="section-head">
            <h3>切片内容</h3>
            <span>{{ chunks.length }} 段</span>
          </div>

          <div v-if="loadingChunks" class="chunk-loading" aria-live="polite">
            <n-spin :size="18" />
            <span>正在读取切片…</span>
          </div>

          <n-alert v-else-if="chunksError" type="error" :show-icon="false" closable @close="chunksError = ''">
            {{ chunksError }}
          </n-alert>

          <div v-else-if="chunks.length === 0" class="chunk-empty">
            <KvIcon name="layers" :size="22" :stroke-width="1.6" />
            <p>暂无切片</p>
            <span>文档仍在处理时还没有切片，稍后刷新或重新处理</span>
          </div>

          <div v-else class="chunk-list">
            <article v-for="chunk in chunks" :key="chunk.id" class="chunk-item">
              <header class="chunk-head">
                <span class="chunk-index">#{{ chunk.chunkIndex + 1 }}</span>
                <span v-if="chunk.headingPath" class="chunk-heading" :title="chunk.headingPath">{{ chunk.headingPath }}</span>
                <span class="chunk-len">{{ chunk.content.length }} 字</span>
              </header>
              <p class="chunk-text" :class="{ expanded: expanded[chunk.id] }">{{ chunk.content }}</p>
              <button
                v-if="chunk.content.length > 160"
                type="button"
                class="chunk-toggle"
                @click="toggleChunk(chunk.id)"
              >
                {{ expanded[chunk.id] ? '收起' : '展开全文' }}
              </button>
            </article>
          </div>
        </section>
      </div>
    </n-drawer-content>
  </n-drawer>
</template>

<script setup>
import { computed, onBeforeUnmount, ref, watch } from 'vue'
import { NAlert, NButton, NDrawer, NDrawerContent, NSpin } from 'naive-ui'
import KvIcon from './KvIcon.vue'
import { documentApi } from '../api/documents'

const props = defineProps({
  show: { type: Boolean, default: false },
  document: { type: Object, default: null },
})
const emit = defineEmits(['update:show'])

const chunks = ref([])
const loadingChunks = ref(false)
const chunksError = ref('')
const expanded = ref({})
const previewing = ref(false)
const downloading = ref(false)
const previewMode = ref('')
const previewUrl = ref('')
const previewText = ref('')
const previewError = ref('')

const drawerWidth = 'min(640px, 100vw)'

// 与后端 DocumentSourceTypeEnum 一致：0 Pdf / 1 Word / 2 Excel / 3 Markdown / 4 Html
const sourceLabel = computed(() => ['PDF', 'DOCX', 'XLSX', 'MD', 'HTML'][props.document?.sourceType] ?? 'FILE')

// 与后端 DocumentStatusEnum 一致：0 Pending / 1 Processing / 2 Ready / 3 Failed
const statusText = computed(() => ['待处理', '解析中', '已就绪', '失败'][props.document?.status] ?? '未知')

// Office 文件浏览器内无法直接渲染，点击预览即下载
const previewable = computed(() => props.document && props.document.sourceType !== 1 && props.document.sourceType !== 2)

watch(
  () => [props.show, props.document?.id],
  ([show]) => {
    if (!show || !props.document) return
    loadChunks()
  },
)

/**
 * 拉取切片列表；失败时保留错误提示，不阻塞原件预览
 */
async function loadChunks() {
  loadingChunks.value = true
  chunksError.value = ''
  expanded.value = {}
  try {
    chunks.value = await documentApi.getChunks(props.document.id)
  } catch (e) {
    chunks.value = []
    chunksError.value = await describeError(e)
  } finally {
    loadingChunks.value = false
  }
}

/**
 * 展开 / 收起单条切片
 * @param {string} id 切片 ID
 */
function toggleChunk(id) {
  expanded.value = { ...expanded.value, [id]: !expanded.value[id] }
}

/** 预览原件：PDF / HTML 走 iframe，Markdown 走文本，Office 直接下载 */
async function preview() {
  if (!props.document) return
  if (!previewable.value) {
    await download()
    return
  }

  closePreview()
  previewing.value = true
  previewError.value = ''
  try {
    const blob = await documentApi.content(props.document.id)
    if (props.document.sourceType === 3) {
      previewText.value = await blob.text()
      previewMode.value = 'text'
    } else {
      previewUrl.value = URL.createObjectURL(blob)
      previewMode.value = 'frame'
    }
  } catch (e) {
    previewError.value = await describeError(e)
  } finally {
    previewing.value = false
  }
}

/** 下载原件：blob 生成临时链接，点击后立即回收 */
async function download() {
  if (!props.document || downloading.value) return
  downloading.value = true
  previewError.value = ''
  try {
    const blob = await documentApi.content(props.document.id, true)
    const url = URL.createObjectURL(blob)
    const link = document.createElement('a')
    link.href = url
    link.download = downloadName()
    link.click()
    window.setTimeout(() => URL.revokeObjectURL(url), 1000)
  } catch (e) {
    previewError.value = await describeError(e)
  } finally {
    downloading.value = false
  }
}

/** 关闭预览并释放 ObjectURL，避免 blob 长期占内存 */
function closePreview() {
  if (previewUrl.value) URL.revokeObjectURL(previewUrl.value)
  previewUrl.value = ''
  previewText.value = ''
  previewMode.value = ''
}

onBeforeUnmount(closePreview)

/**
 * 下载文件名：标题 + 扩展名（DocumentDto 不返回原始文件名）
 * @returns {string} 文件名
 */
function downloadName() {
  const extension = ['pdf', 'docx', 'xlsx', 'md', 'html'][props.document.sourceType] ?? 'txt'
  return `${props.document.title}.${extension}`
}

/**
 * 列表时间：今天显示时分，今年显示月日，更早显示完整日期
 * @param {string} value ISO 时间
 * @returns {string} 展示文本
 */
function formatTime(value) {
  if (!value) return '—'
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return '—'
  const now = new Date()
  if (date.toDateString() === now.toDateString()) {
    return date.toLocaleTimeString('zh-CN', { hour: '2-digit', minute: '2-digit' })
  }
  if (date.getFullYear() === now.getFullYear()) {
    return `${String(date.getMonth() + 1).padStart(2, '0')}-${String(date.getDate()).padStart(2, '0')}`
  }
  return date.toLocaleDateString('zh-CN')
}

/**
 * 把未知错误整理成一行可读文案；blob 响应需要先读成文本再解析
 * @param {any} e 错误对象
 * @returns {Promise<string>} 错误文案
 */
async function describeError(e) {
  const data = e?.response?.data
  if (data instanceof Blob) {
    try {
      const parsed = JSON.parse(await data.text())
      return parsed?.error?.message || parsed?.message || '操作失败'
    } catch {
      return '操作失败'
    }
  }
  return data?.error?.message || e?.message || '操作失败'
}
</script>

<style scoped>
.drawer-head {
  display: flex;
  align-items: flex-start;
  gap: 10px;
  min-width: 0;
}

.drawer-title-wrap {
  display: flex;
  flex-direction: column;
  gap: 5px;
  min-width: 0;
}

.drawer-title {
  margin: 0;
  font-size: 16px;
  font-weight: 650;
  letter-spacing: -0.01em;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.file-badge {
  flex: none;
  padding: 3px 7px;
  border-radius: 6px;
  background: var(--kv-accent-soft);
  color: var(--kv-accent);
  font-size: 10.5px;
  font-weight: 700;
  letter-spacing: 0.04em;
}

.status-pill {
  display: inline-flex;
  align-items: center;
  gap: 5px;
  align-self: flex-start;
  padding: 1px 8px;
  border-radius: 999px;
  font-size: 11px;
  font-weight: 600;
}

.status-dot {
  width: 5px;
  height: 5px;
  border-radius: 50%;
  background: currentColor;
}

.status-0 {
  background: #f1f2ee;
  color: var(--kv-text-2);
}

.status-1 {
  background: var(--kv-accent-soft);
  color: var(--kv-accent);
}

.status-2 {
  background: var(--kv-primary-soft);
  color: var(--kv-primary);
}

.status-3 {
  background: var(--kv-danger-soft);
  color: var(--kv-danger);
}

.detail-body {
  display: flex;
  flex-direction: column;
  gap: 16px;
  padding-bottom: 8px;
}

.meta-card {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 10px 14px;
  padding: 14px 16px;
  border: 1px solid var(--kv-border);
  border-radius: var(--kv-radius-md);
  background: var(--kv-surface-soft);
}

.meta-item {
  display: flex;
  flex-direction: column;
  gap: 2px;
  min-width: 0;
}

.meta-label {
  font-size: 11px;
  color: var(--kv-text-3);
}

.meta-value {
  font-size: 13px;
  font-weight: 550;
  color: var(--kv-text);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.detail-actions {
  display: flex;
  gap: 10px;
}

.preview-card {
  border: 1px solid var(--kv-border);
  border-radius: var(--kv-radius-md);
  overflow: hidden;
  background: var(--kv-surface);
}

.preview-head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 10px;
  padding: 8px 12px;
  border-bottom: 1px solid var(--kv-border);
  background: var(--kv-surface-soft);
  font-size: 12px;
  font-weight: 600;
  color: var(--kv-text-2);
}

.preview-frame {
  display: block;
  width: 100%;
  height: 440px;
  border: 0;
  background: #fff;
}

.preview-text {
  margin: 0;
  max-height: 440px;
  overflow: auto;
  padding: 14px 16px;
  font-family: "SFMono-Regular", Consolas, "Liberation Mono", monospace;
  font-size: 12.5px;
  line-height: 1.7;
  color: var(--kv-text-2);
  white-space: pre-wrap;
  word-break: break-word;
}

.chunk-section {
  display: flex;
  flex-direction: column;
  gap: 10px;
}

.section-head {
  display: flex;
  align-items: baseline;
  justify-content: space-between;
  gap: 10px;
}

.section-head h3 {
  margin: 0;
  font-size: 13.5px;
  font-weight: 650;
}

.section-head span {
  font-size: 11.5px;
  color: var(--kv-text-3);
}

.chunk-loading {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 20px 4px;
  font-size: 12.5px;
  color: var(--kv-text-3);
}

.chunk-empty {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 5px;
  padding: 30px 16px;
  border: 1px dashed var(--kv-border-strong);
  border-radius: var(--kv-radius-md);
  color: var(--kv-text-3);
  text-align: center;
}

.chunk-empty p {
  margin: 0;
  font-size: 13px;
  font-weight: 600;
  color: var(--kv-text-2);
}

.chunk-empty span {
  font-size: 11.5px;
  line-height: 1.6;
}

.chunk-list {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.chunk-item {
  padding: 12px 14px;
  border: 1px solid var(--kv-border);
  border-radius: var(--kv-radius-sm);
  background: var(--kv-surface);
  transition: border-color 0.16s ease;
}

.chunk-item:hover {
  border-color: var(--kv-border-strong);
}

.chunk-head {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-bottom: 6px;
}

.chunk-index {
  flex: none;
  padding: 1px 7px;
  border-radius: 6px;
  background: var(--kv-primary-soft);
  color: var(--kv-primary);
  font-size: 10.5px;
  font-weight: 700;
}

.chunk-heading {
  flex: 1;
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  font-size: 11.5px;
  color: var(--kv-text-3);
}

.chunk-len {
  flex: none;
  font-size: 10.5px;
  color: var(--kv-text-3);
}

.chunk-text {
  margin: 0;
  display: -webkit-box;
  -webkit-line-clamp: 4;
  -webkit-box-orient: vertical;
  overflow: hidden;
  font-size: 12.5px;
  line-height: 1.75;
  color: var(--kv-text-2);
  white-space: pre-wrap;
  word-break: break-word;
}

.chunk-text.expanded {
  display: block;
  overflow: visible;
}

.chunk-toggle {
  margin-top: 6px;
  padding: 0;
  border: 0;
  background: none;
  color: var(--kv-primary);
  font-size: 11.5px;
  font-weight: 600;
  cursor: pointer;
}

.chunk-toggle:hover {
  color: var(--kv-primary-strong);
  text-decoration: underline;
}

.chunk-toggle:focus-visible {
  outline: 2px solid var(--kv-primary);
  outline-offset: 2px;
  border-radius: 4px;
}

@media (max-width: 560px) {
  .meta-card {
    grid-template-columns: minmax(0, 1fr);
  }

  .preview-frame {
    height: 320px;
  }
}
</style>

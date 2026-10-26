<template>
  <div class="docs-pane">
    <div class="docs-toolbar">
      <div class="docs-title">
        <h3>资料文档</h3>
        <p>支持 PDF、Word、Excel、Markdown、HTML；可一次多选，同名文件会被拒绝</p>
      </div>
      <div class="docs-toolbar-actions">
        <span v-if="store.uploadingCount > 0" class="uploading" role="status">
          <n-spin :size="13" />
          正在上传 {{ store.uploadingCount }} 个文件
        </span>
        <n-upload
          :custom-request="customRequest"
          multiple
          :show-file-list="false"
          :disabled="store.uploadingCount > 0"
        >
          <n-button type="primary" size="small" :loading="store.uploadingCount > 0">
            <template #icon><KvIcon name="upload" :size="14" /></template>
            上传文档
          </n-button>
        </n-upload>
      </div>
    </div>

    <n-alert v-if="store.error" type="error" :show-icon="false" closable @close="store.error = ''">
      {{ store.error }}
    </n-alert>

    <div class="doc-list">
      <article v-for="doc in store.documents" :key="doc.id" class="doc-item">
        <div class="doc-item-main">
          <span class="file-badge">{{ sourceLabel(doc.sourceType) }}</span>
          <div class="doc-info">
            <div class="doc-line">
              <button type="button" class="doc-title" :title="doc.title" @click="openDetail(doc)">{{ doc.title }}</button>
              <span class="status-pill" :class="'status-' + doc.status">
                <span class="status-dot"></span>
                {{ statusText(doc.status) }}
              </span>
            </div>
            <div v-if="doc.ingestAttemptCount > 1" class="doc-attempts">已尝试 {{ doc.ingestAttemptCount }} 次</div>
            <div v-if="doc.status === 3 && doc.errorMessage" class="doc-error">{{ doc.errorMessage }}</div>
          </div>
        </div>
        <div class="doc-actions">
          <n-button size="tiny" quaternary :disabled="doc.status === 1" @click="reprocess(doc)">
            <template #icon><KvIcon name="rotate" :size="13" /></template>
            重新处理
          </n-button>
          <n-popconfirm @positive-click="confirmRemove(doc)">
            <template #trigger>
              <n-button size="tiny" quaternary type="error">
                <template #icon><KvIcon name="trash" :size="13" /></template>
                删除
              </n-button>
            </template>
            删除后不可恢复（切片、向量与原件一并清理）。确认删除「{{ doc.title }}」？
          </n-popconfirm>
        </div>
      </article>

      <div v-if="store.documents.length === 0" class="doc-empty">
        <KvIcon name="file-text" :size="22" :stroke-width="1.6" />
        <p>还没有文档</p>
        <span>上传第一份资料，系统会自动解析、切片并建立索引</span>
      </div>
    </div>

    <DocumentDetailDrawer v-model:show="detailVisible" :document="detailDoc" />
  </div>
</template>

<script setup>
import { ref } from 'vue'
import { NAlert, NButton, NPopconfirm, NSpin, NUpload, useMessage } from 'naive-ui'
import KvIcon from './KvIcon.vue'
import DocumentDetailDrawer from './DocumentDetailDrawer.vue'
import { useDocumentStore } from '../stores/documents'

const store = useDocumentStore()
const message = useMessage()

const detailVisible = ref(false)
const detailDoc = ref(null)

// 状态枚举落库为 int：0 Pending / 1 Processing / 2 Ready / 3 Failed
const statusText = (status) => ['待处理', '解析中', '已就绪', '失败'][status] ?? '未知'

// 来源枚举与后端 DocumentSourceTypeEnum 一致：0 Pdf / 1 Word / 2 Excel / 3 Markdown / 4 Html
const sourceLabel = (sourceType) => ['PDF', 'DOCX', 'XLSX', 'MD', 'HTML'][sourceType] ?? 'FILE'

/**
 * 打开文档详情（切片与原件预览）
 * @param {object} doc 文档
 */
function openDetail(doc) {
  detailDoc.value = doc
  detailVisible.value = true
}

/**
 * n-upload 的自定义上传：逐个文件走 store，失败时把文件名与后端原因一并提示
 * @param {{file: {file: File, name: string}, onFinish: Function, onError: Function}} options 上传回调参数
 */
async function customRequest({ file, onFinish, onError }) {
  const result = await store.upload(file.file)
  if (result.ok) {
    onFinish()
  } else {
    message.error(`${file.name}：${result.message}`)
    onError()
  }
}

/**
 * 重新处理文档
 * @param {object} doc 文档
 */
async function reprocess(doc) {
  try {
    await store.reprocessDocument(doc.id)
    message.success('已重新排队')
  } catch (e) {
    message.error(describeError(e))
  }
}

/**
 * 确认删除：气泡立即关闭，处理中状态由全局遮罩承担；失败时弹错误提示
 * @param {object} doc 文档
 */
function confirmRemove(doc) {
  remove(doc)
}

/**
 * 删除文档
 * @param {object} doc 文档
 */
async function remove(doc) {
  if (store.deletingId) return
  try {
    await store.removeDocument(doc.id)
    message.success('已删除')
  } catch (e) {
    message.error(describeError(e))
  }
}

/**
 * 把未知错误整理成一行可读文案
 * @param {any} e 错误对象
 * @returns {string} 错误文案
 */
function describeError(e) {
  return e?.response?.data?.error?.message || e?.message || '操作失败'
}
</script>

<style scoped>
.docs-pane {
  flex: 1;
  min-height: 0;
  display: flex;
  flex-direction: column;
  gap: 12px;
  padding: 18px 24px 20px;
}

.docs-toolbar {
  flex: none;
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 16px;
}

.docs-title {
  min-width: 0;
}

.docs-title h3 {
  margin: 0;
  font-size: 14.5px;
  font-weight: 650;
  letter-spacing: -0.01em;
  color: var(--kv-text);
}

.docs-title p {
  margin: 3px 0 0;
  font-size: 11.5px;
  line-height: 1.55;
  color: var(--kv-text-3);
}

.docs-toolbar-actions {
  flex: none;
  display: flex;
  align-items: center;
  gap: 10px;
}

.uploading {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  font-size: 11.5px;
  color: var(--kv-text-3);
}

.doc-list {
  flex: 1;
  min-height: 0;
  overflow-y: auto;
  display: flex;
  flex-direction: column;
  gap: 8px;
  margin: 0 -4px;
  padding: 0 4px 2px;
}

.doc-item {
  padding: 11px 12px;
  border: 1px solid var(--kv-border);
  border-radius: 12px;
  background: var(--kv-surface-soft);
  transition: border-color 0.18s ease, background 0.18s ease, box-shadow 0.18s ease;
}

.doc-item:hover {
  border-color: var(--kv-border-strong);
  background: #ffffff;
  box-shadow: var(--kv-shadow-sm);
}

.doc-item-main {
  display: flex;
  align-items: flex-start;
  gap: 10px;
}

.file-badge {
  width: 32px;
  height: 32px;
  flex: none;
  display: grid;
  place-items: center;
  border: 1px solid var(--kv-border);
  border-radius: 9px;
  background: #ffffff;
  color: var(--kv-primary);
  font-size: 9px;
  font-weight: 700;
  letter-spacing: 0.02em;
}

.doc-info {
  flex: 1;
  min-width: 0;
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.doc-line {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 8px;
}

.doc-title {
  flex: 1;
  min-width: 0;
  padding: 0;
  border: 0;
  background: none;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  font-family: inherit;
  font-size: 13px;
  font-weight: 600;
  text-align: left;
  color: var(--kv-text);
  cursor: pointer;
}

.doc-title:hover {
  color: var(--kv-primary-strong);
  text-decoration: underline;
  text-underline-offset: 3px;
}

.doc-title:focus-visible {
  outline: 2px solid var(--kv-primary);
  outline-offset: 2px;
  border-radius: 4px;
}

.status-pill {
  flex: none;
  display: inline-flex;
  align-items: center;
  gap: 5px;
  padding: 2px 7px;
  border: 1px solid var(--kv-border);
  border-radius: 999px;
  background: #ffffff;
  font-size: 10.5px;
  color: var(--kv-text-2);
}

.status-dot {
  width: 5px;
  height: 5px;
  border-radius: 50%;
  background: #a9b0aa;
}

.status-1 {
  color: #3b6f8f;
}

.status-1 .status-dot {
  background: #3b6f8f;
}

.status-2 {
  color: var(--kv-success);
}

.status-2 .status-dot {
  background: var(--kv-success);
}

.status-3 {
  color: var(--kv-danger);
}

.status-3 .status-dot {
  background: var(--kv-danger);
}

.doc-attempts {
  font-size: 11px;
  color: var(--kv-text-3);
}

.doc-error {
  margin-top: 2px;
  font-size: 11px;
  line-height: 1.5;
  word-break: break-all;
  color: var(--kv-danger);
}

.doc-actions {
  display: flex;
  justify-content: flex-end;
  gap: 2px;
  margin-top: 8px;
  padding-top: 7px;
  border-top: 1px dashed var(--kv-border);
}

.doc-empty {
  flex: 1;
  min-height: 180px;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 6px;
  padding: 28px 12px;
  border: 1px dashed var(--kv-border-strong);
  border-radius: 12px;
  text-align: center;
  color: var(--kv-text-3);
}

.doc-empty p {
  margin: 0;
  font-size: 13px;
  font-weight: 600;
  color: var(--kv-text-2);
}

.doc-empty span {
  font-size: 11px;
  line-height: 1.5;
}

@media (max-width: 900px) {
  .docs-pane {
    flex: none;
    padding: 16px;
  }

  .doc-list {
    overflow: visible;
  }

  .doc-empty {
    flex: none;
  }
}

@media (max-width: 640px) {
  .docs-toolbar {
    flex-direction: column;
    align-items: stretch;
    gap: 10px;
  }

  .docs-toolbar-actions {
    justify-content: flex-end;
  }
}
</style>

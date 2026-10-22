<template>
  <div class="doc-panel">
    <div class="panel-head">
      <div>
        <h2>资料库</h2>
        <p>上传后自动解析、切片并建立索引</p>
      </div>
      <span class="doc-count">{{ store.documents.length }}</span>
    </div>

    <n-select
      v-model:value="store.knowledgeBaseId"
      :options="kbOptions"
      size="small"
      class="kb-select"
      aria-label="选择知识库"
      @update:value="store.refresh()"
    />

    <n-upload :custom-request="customRequest" :show-file-list="false" :disabled="store.uploading" class="upload-wrap">
      <n-button class="upload-button" block :loading="store.uploading">
        <template #icon><KvIcon name="upload" :size="17" /></template>
        上传文档
      </n-button>
    </n-upload>
    <p class="upload-hint">支持 PDF、Word、Excel、Markdown、HTML</p>

    <n-alert v-if="store.error" type="error" :show-icon="false" closable @close="store.error = ''">
      {{ store.error }}
    </n-alert>

    <div class="doc-list">
      <article v-for="doc in store.documents" :key="doc.id" class="doc-item">
        <div class="doc-item-main">
          <span class="file-badge">{{ sourceLabel(doc.sourceType) }}</span>
          <div class="doc-info">
            <div class="doc-line">
              <span class="doc-title" :title="doc.title">{{ doc.title }}</span>
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
          <n-popconfirm @positive-click="remove(doc)">
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
        <span>上传一份资料，构建你的第一个知识索引</span>
      </div>
    </div>
  </div>
</template>

<script setup>
import { computed } from 'vue'
import { NSelect, NUpload, NButton, NAlert, NPopconfirm, useMessage } from 'naive-ui'
import KvIcon from './KvIcon.vue'
import { useDocumentStore } from '../stores/documents'

const store = useDocumentStore()
const message = useMessage()

const kbOptions = computed(() =>
  store.knowledgeBases.map((kb) => ({ label: kb.name, value: kb.id })),
)

// 状态枚举落库为 int：0 Pending / 1 Processing / 2 Ready / 3 Failed
const statusText = (status) => ['待处理', '解析中', '已就绪', '失败'][status] ?? '未知'

// 来源枚举与后端 DocumentSourceTypeEnum 一致：0 Pdf / 1 Word / 2 Excel / 3 Markdown / 4 Html
const sourceLabel = (sourceType) => ['PDF', 'DOCX', 'XLSX', 'MD', 'HTML'][sourceType] ?? 'FILE'

/**
 * n-upload 的自定义上传：交给 store 走我们自己的 axios 实例（带 Bearer）。
 * @param {{file: {file: File}, onFinish: Function, onError: Function}} options 上传回调参数
 */
async function customRequest({ file, onFinish, onError }) {
  const ok = await store.upload(file.file)
  if (ok) {
    message.success('已提交解析')
    onFinish()
  } else {
    onError()
  }
}

/**
 * 重新处理文档。
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
 * 删除文档。
 * @param {object} doc 文档
 */
async function remove(doc) {
  try {
    await store.removeDocument(doc.id)
    message.success('已删除')
  } catch (e) {
    message.error(describeError(e))
  }
}

/**
 * 把未知错误整理成一行可读文案。
 * @param {any} e 错误对象
 * @returns {string} 错误文案
 */
function describeError(e) {
  return e?.response?.data?.error?.message || e?.message || '操作失败'
}
</script>

<style scoped>
.doc-panel {
  height: 100%;
  display: flex;
  flex-direction: column;
  gap: 12px;
  overflow: hidden;
  padding: 20px 18px;
  border: 1px solid var(--kv-border);
  border-radius: var(--kv-radius-lg);
  background: var(--kv-surface);
  box-shadow: var(--kv-shadow-md);
}

.panel-head {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 12px;
}

.panel-head h2 {
  margin: 0;
  font-size: 16px;
  font-weight: 650;
  letter-spacing: -0.01em;
}

.panel-head p {
  margin: 3px 0 0;
  font-size: 12px;
  line-height: 1.5;
  color: var(--kv-text-3);
}

.doc-count {
  flex: none;
  min-width: 26px;
  height: 24px;
  padding: 0 8px;
  display: inline-grid;
  place-items: center;
  border-radius: 999px;
  background: var(--kv-primary-soft);
  color: var(--kv-primary);
  font-size: 12px;
  font-weight: 600;
}

.kb-select {
  width: 100%;
}

.upload-wrap {
  display: block;
}

.upload-wrap :deep(.n-upload) {
  display: block;
  width: 100%;
}

.upload-wrap :deep(.n-upload-trigger) {
  display: block;
  width: 100%;
}

.upload-button {
  height: 52px;
  border: 1px dashed #b9cfc8;
  border-radius: 12px;
  background: var(--kv-primary-soft);
  color: var(--kv-primary);
  font-weight: 600;
  box-shadow: none;
}

.upload-button:hover {
  border-color: var(--kv-primary);
  background: #eef8f5;
  color: var(--kv-primary-strong);
}

.upload-hint {
  margin: -5px 0 0;
  font-size: 11px;
  text-align: center;
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
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  font-size: 13px;
  font-weight: 600;
  color: var(--kv-text);
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
  min-height: 150px;
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
</style>

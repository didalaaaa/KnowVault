<template>
  <div class="doc-panel">
    <div class="row">
      <n-select
        v-model:value="store.knowledgeBaseId"
        :options="kbOptions"
        size="small"
        @update:value="store.refresh()"
      />
    </div>

    <n-upload :custom-request="customRequest" :show-file-list="false" :disabled="store.uploading">
      <n-button size="small" type="primary" :loading="store.uploading" block>
        上传文档（PDF/Word/Excel/MD/HTML）
      </n-button>
    </n-upload>

    <n-alert v-if="store.error" type="error" :show-icon="false" closable @close="store.error = ''">
      {{ store.error }}
    </n-alert>

    <n-list bordered>
      <n-list-item v-for="doc in store.documents" :key="doc.id">
        <div class="doc-row">
          <span class="doc-title" :title="doc.title">{{ doc.title }}</span>
          <n-tag size="tiny" :type="statusType(doc.status)">{{ statusText(doc.status) }}</n-tag>
        </div>
        <div v-if="doc.ingestAttemptCount > 1" class="doc-attempts">已尝试 {{ doc.ingestAttemptCount }} 次</div>
        <div v-if="doc.status === 3 && doc.errorMessage" class="doc-error">{{ doc.errorMessage }}</div>
        <div class="doc-actions">
          <n-button
            size="tiny"
            quaternary
            :disabled="doc.status === 1"
            @click="reprocess(doc)"
          >
            重新处理
          </n-button>
          <n-popconfirm @positive-click="remove(doc)">
            <template #trigger>
              <n-button size="tiny" quaternary type="error">删除</n-button>
            </template>
            删除后不可恢复（切片、向量与原件一并清理）。确认删除「{{ doc.title }}」？
          </n-popconfirm>
        </div>
      </n-list-item>
      <n-empty v-if="store.documents.length === 0" description="还没有文档" />
    </n-list>
  </div>
</template>

<script setup>
import { computed } from 'vue'
import { NSelect, NUpload, NButton, NList, NListItem, NTag, NAlert, NEmpty, NPopconfirm, useMessage } from 'naive-ui'
import { useDocumentStore } from '../stores/documents'

const store = useDocumentStore()
const message = useMessage()

const kbOptions = computed(() =>
  store.knowledgeBases.map((kb) => ({ label: kb.name, value: kb.id })),
)

// 状态枚举落库为 int：0 Pending / 1 Processing / 2 Ready / 3 Failed
const statusText = (status) => ['待处理', '解析中', '已就绪', '失败'][status] ?? '未知'
const statusType = (status) => ['default', 'info', 'success', 'error'][status] ?? 'default'

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
  display: flex;
  flex-direction: column;
  gap: 12px;
}
.row {
  display: flex;
  gap: 8px;
}
.doc-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 8px;
}
.doc-title {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
.doc-error {
  color: #d03050;
  font-size: 12px;
  margin-top: 4px;
  word-break: break-all;
}
.doc-attempts {
  color: #888;
  font-size: 12px;
  margin-top: 4px;
}
.doc-actions {
  display: flex;
  justify-content: flex-end;
  gap: 4px;
  margin-top: 4px;
}
</style>

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
        <div v-if="doc.status === 3 && doc.errorMessage" class="doc-error">{{ doc.errorMessage }}</div>
      </n-list-item>
      <n-empty v-if="store.documents.length === 0" description="还没有文档" />
    </n-list>
  </div>
</template>

<script setup>
import { computed } from 'vue'
import { NSelect, NUpload, NButton, NList, NListItem, NTag, NAlert, NEmpty, useMessage } from 'naive-ui'
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
</style>

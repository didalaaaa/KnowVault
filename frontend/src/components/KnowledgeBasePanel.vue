<template>
  <div class="kb-panel">
    <div class="kb-header">
      <span class="kb-title">知识库</span>
      <n-button size="small" type="primary" @click="openCreate">新建</n-button>
    </div>

    <n-list bordered>
      <n-list-item v-for="kb in documents.knowledgeBases" :key="kb.id">
        <n-thing :title="kb.name">
          <template #description>
            <span>{{ kb.description || '（无描述）' }}</span>
          </template>
          <template #footer>
            <span class="kb-meta">已就绪文档 {{ kb.readyDocumentCount }}</span>
            <n-tag v-if="kb.id === defaultKnowledgeBaseId" size="small" type="success" style="margin-left: 8px">
              默认
            </n-tag>
          </template>
        </n-thing>
        <template #suffix>
          <n-button size="tiny" quaternary @click="openEdit(kb)">编辑</n-button>
          <n-popconfirm @positive-click="remove(kb)">
            <template #trigger>
              <n-button size="tiny" quaternary :disabled="kb.id === defaultKnowledgeBaseId">删除</n-button>
            </template>
            删除后不可恢复；知识库下还有文档时后端会拒绝。确认删除「{{ kb.name }}」？
          </n-popconfirm>
        </template>
      </n-list-item>
    </n-list>

    <n-modal
      v-model:show="formVisible"
      preset="card"
      :title="editingId ? '编辑知识库' : '新建知识库'"
      style="width: 420px"
    >
      <n-form :model="form">
        <n-form-item label="名称">
          <n-input v-model:value="form.name" maxlength="128" placeholder="例如：产品手册" />
        </n-form-item>
        <n-form-item label="描述">
          <n-input v-model:value="form.description" type="textarea" maxlength="512" placeholder="可选" />
        </n-form-item>
      </n-form>
      <template #footer>
        <div class="kb-form-footer">
          <n-button @click="formVisible = false">取消</n-button>
          <n-button type="primary" :loading="saving" :disabled="!form.name.trim()" @click="save">保存</n-button>
        </div>
      </template>
    </n-modal>
  </div>
</template>

<script setup>
import { ref, reactive } from 'vue'
import {
  NButton, NList, NListItem, NThing, NTag, NModal, NForm, NFormItem, NInput, NPopconfirm, useMessage,
} from 'naive-ui'
import { useDocumentStore } from '../stores/documents'

const documents = useDocumentStore()
const message = useMessage()

// 与后端 KnowVaultConsts.DefaultKnowledgeBaseId 保持一致；默认库禁止删除
const defaultKnowledgeBaseId = '00000000-0000-0000-0000-000000000001'

const formVisible = ref(false)
const saving = ref(false)
const editingId = ref('')
const form = reactive({ name: '', description: '' })

/** 打开「新建」表单。 */
function openCreate() {
  editingId.value = ''
  form.name = ''
  form.description = ''
  formVisible.value = true
}

/**
 * 打开「编辑」表单。
 * @param {object} kb 知识库
 */
function openEdit(kb) {
  editingId.value = kb.id
  form.name = kb.name
  form.description = kb.description || ''
  formVisible.value = true
}

/** 保存（新建或编辑）；失败时弹后端的中文文案。 */
async function save() {
  saving.value = true
  try {
    if (editingId.value) {
      await documents.updateKnowledgeBase({ id: editingId.value, name: form.name, description: form.description })
      message.success('已保存')
    } else {
      await documents.createKnowledgeBase({ name: form.name, description: form.description })
      message.success('已新建')
    }
    formVisible.value = false
  } catch (e) {
    message.error(describeError(e))
  } finally {
    saving.value = false
  }
}

/**
 * 删除知识库（非空库与默认库由后端拒绝）。
 * @param {object} kb 知识库
 */
async function remove(kb) {
  try {
    await documents.removeKnowledgeBase(kb.id)
    message.success('已删除')
  } catch (e) {
    message.error(describeError(e))
  }
}

/**
 * 把未知错误整理成一行可读文案（ABP 的 UserFriendlyException 文案在 error.message）。
 * @param {any} e 错误对象
 * @returns {string} 错误文案
 */
function describeError(e) {
  return e?.response?.data?.error?.message || e?.message || '操作失败'
}
</script>

<style scoped>
.kb-panel { max-width: 860px; margin: 0 auto; display: flex; flex-direction: column; gap: 16px; }
.kb-header { display: flex; align-items: center; justify-content: space-between; }
.kb-title { font-weight: 600; }
.kb-meta { color: #888; font-size: 12px; }
.kb-form-footer { display: flex; justify-content: flex-end; gap: 8px; }
</style>

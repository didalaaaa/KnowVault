<template>
  <div class="kb-panel">
    <div class="kb-header">
      <div>
        <span class="kb-eyebrow">Workspace</span>
        <h2 class="kb-title">知识库</h2>
        <p class="kb-subtitle">按团队或主题组织文档，并通过成员角色控制访问</p>
      </div>
      <n-button type="primary" class="kb-create" @click="openCreate">
        <template #icon><KvIcon name="plus" :size="15" /></template>
        新建
      </n-button>
    </div>

    <div class="kb-grid">
      <article v-for="kb in documents.knowledgeBases" :key="kb.id" class="kb-card">
        <div class="kb-card-head">
          <span class="kb-icon"><KvIcon name="layers" :size="18" :stroke-width="1.7" /></span>
          <div class="kb-name-row">
            <h3 :title="kb.name">{{ kb.name }}</h3>
            <n-tag v-if="kb.id === defaultKnowledgeBaseId" size="small" type="success" :bordered="false">默认</n-tag>
          </div>
        </div>

        <p class="kb-desc">{{ kb.description || '（无描述）' }}</p>

        <div class="kb-card-meta">
          <span class="meta-item">
            <KvIcon name="file-text" :size="13" />
            已就绪 <strong>{{ kb.readyDocumentCount }}</strong> 篇
          </span>
        </div>

        <div class="kb-card-actions">
          <n-button size="tiny" quaternary :disabled="kb.id === defaultKnowledgeBaseId" @click="openMembers(kb)">
            <template #icon><KvIcon name="users" :size="13" /></template>
            成员
          </n-button>
          <n-button size="tiny" quaternary @click="openSettings(kb)">
            <template #icon><KvIcon name="settings" :size="13" /></template>
            设置
          </n-button>
          <n-button size="tiny" quaternary @click="openEdit(kb)">
            <template #icon><KvIcon name="pencil" :size="13" /></template>
            编辑
          </n-button>
          <span class="spacer"></span>
          <n-popconfirm @positive-click="remove(kb)">
            <template #trigger>
              <n-button size="tiny" quaternary type="error" :disabled="kb.id === defaultKnowledgeBaseId">
                <template #icon><KvIcon name="trash" :size="13" /></template>
                删除
              </n-button>
            </template>
            删除后不可恢复；知识库下还有文档时后端会拒绝。确认删除「{{ kb.name }}」？
          </n-popconfirm>
        </div>
      </article>

      <div v-if="documents.knowledgeBases.length === 0" class="kb-empty">
        <KvIcon name="book-open" :size="22" :stroke-width="1.6" />
        <p>还没有知识库</p>
        <span>新建一个知识库，开始整理你的资料</span>
      </div>
    </div>

    <n-modal
      v-model:show="formVisible"
      preset="card"
      :title="editingId ? '编辑知识库' : '新建知识库'"
      class="kv-modal"
      style="width: min(420px, calc(100vw - 32px))"
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

    <n-modal
      v-model:show="settingsVisible"
      preset="card"
      :title="'知识库设置 · ' + (settingsKb?.name || '')"
      class="kv-modal"
      style="width: min(480px, calc(100vw - 32px))"
    >
      <n-alert type="info" :bordered="false" class="settings-tip">
        修改仅对新上传的文档与新提问生效，已入库文档不会被重新切片
      </n-alert>
      <n-form :model="settingsForm" label-placement="top">
        <n-form-item label="切片长度（字符）" feedback="单片目标长度，数值越小引用片段越细">
          <n-input-number
            v-model:value="settingsForm.chunkSizeChars"
            :min="200"
            :max="4000"
            :step="100"
            placeholder="留空使用全局 1000"
            style="width: 100%"
            clearable
          />
        </n-form-item>
        <n-form-item label="切片重叠（字符）" feedback="相邻切片保留的上下文，不能超过切片长度的一半">
          <n-input-number
            v-model:value="settingsForm.chunkOverlapChars"
            :min="0"
            :max="500"
            :step="20"
            placeholder="留空使用全局 120"
            style="width: 100%"
            clearable
          />
        </n-form-item>
        <n-form-item label="检索条数（TopK）" feedback="每次提问最多带入的片段数，1–8">
          <n-input-number
            v-model:value="settingsForm.topK"
            :min="1"
            :max="8"
            placeholder="留空使用全局 5"
            style="width: 100%"
            clearable
          />
        </n-form-item>
        <n-form-item label="系统提示词" feedback="留空使用内置模板；填写后整体替换">
          <n-input
            v-model:value="settingsForm.systemPrompt"
            type="textarea"
            :rows="5"
            maxlength="4000"
            show-count
            placeholder="例如：只依据资料回答，用要点形式输出，每点不超过两句"
          />
        </n-form-item>
      </n-form>
      <template #footer>
        <div class="kb-form-footer">
          <n-button @click="settingsVisible = false">取消</n-button>
          <n-button type="primary" :loading="settingsSaving" @click="saveSettings">保存</n-button>
        </div>
      </template>
    </n-modal>

    <n-modal
      v-model:show="membersVisible"
      preset="card"
      :title="'成员管理 · ' + (activeKb?.name || '')"
      class="kv-modal"
      style="width: min(560px, calc(100vw - 32px))"
    >
      <div class="member-list">
        <div v-if="membersLoading" class="member-empty">正在加载成员…</div>
        <template v-else>
          <div v-for="member in members" :key="member.userId" class="member-row">
            <span class="member-avatar">{{ initial(member.userName) }}</span>
            <span class="member-name">{{ member.userName }}</span>
            <n-tag size="tiny" :bordered="false">{{ roleText(member.role) }}</n-tag>
            <n-button size="tiny" quaternary type="error" @click="removeMember(member)">移除</n-button>
          </div>
          <div v-if="members.length === 0" class="member-empty">还没有成员</div>
        </template>
      </div>

      <n-divider>添加 / 调整成员</n-divider>
      <n-form :model="memberForm" label-placement="left" label-width="70">
        <n-form-item label="用户 ID">
          <n-input v-model:value="memberForm.userId" placeholder="Identity 用户 GUID" />
        </n-form-item>
        <n-form-item label="角色">
          <n-select v-model:value="memberForm.role" :options="roleOptions" />
        </n-form-item>
      </n-form>
      <template #footer>
        <div class="kb-form-footer">
          <n-button @click="membersVisible = false">关闭</n-button>
          <n-button type="primary" :loading="memberSaving" :disabled="!memberForm.userId.trim()" @click="saveMember">
            保存成员
          </n-button>
        </div>
      </template>
    </n-modal>
  </div>
</template>

<script setup>
import { ref, reactive } from 'vue'
import {
  NAlert, NButton, NTag, NModal, NForm, NFormItem, NInput, NInputNumber,
  NPopconfirm, NSelect, NDivider, useMessage,
} from 'naive-ui'
import KvIcon from './KvIcon.vue'
import { useDocumentStore } from '../stores/documents'
import { knowledgeBaseMemberApi } from '../api/knowledgeBaseMembers'

const documents = useDocumentStore()
const message = useMessage()

// 与后端 KnowVaultConsts.DefaultKnowledgeBaseId 保持一致；默认库禁止删除
const defaultKnowledgeBaseId = '00000000-0000-0000-0000-000000000001'

const formVisible = ref(false)
const saving = ref(false)
const editingId = ref('')
const form = reactive({ name: '', description: '' })

const settingsVisible = ref(false)
const settingsSaving = ref(false)
const settingsKb = ref(null)
const settingsForm = reactive({ chunkSizeChars: null, chunkOverlapChars: null, topK: null, systemPrompt: '' })

const membersVisible = ref(false)
const membersLoading = ref(false)
const memberSaving = ref(false)
const members = ref([])
const activeKb = ref(null)
const memberForm = reactive({ userId: '', role: 1 })

const roleOptions = [
  { label: 'Viewer（只读 + 提问）', value: 1 },
  { label: 'Editor（上传 / 删除 / 重处理）', value: 2 },
  { label: 'Owner（管成员 / 删库）', value: 3 },
]

const roleText = (role) => ({ 1: 'Viewer', 2: 'Editor', 3: 'Owner' })[role] ?? '未知'

/** 取用户名首字符作为头像占位。 */
const initial = (name) => (name || '?').trim().charAt(0).toUpperCase()

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

/**
 * 打开「设置」弹窗，用知识库当前值预填。
 * @param {object} kb 知识库
 */
function openSettings(kb) {
  settingsKb.value = kb
  settingsForm.chunkSizeChars = kb.chunkSizeChars ?? null
  settingsForm.chunkOverlapChars = kb.chunkOverlapChars ?? null
  settingsForm.topK = kb.topK ?? null
  settingsForm.systemPrompt = kb.systemPrompt || ''
  settingsVisible.value = true
}

/** 保存切片与检索设置；留空的字段传 null 由后端回落全局配置。 */
async function saveSettings() {
  if (!settingsKb.value) return
  settingsSaving.value = true
  try {
    await documents.updateKnowledgeBaseSettings({
      id: settingsKb.value.id,
      chunkSizeChars: settingsForm.chunkSizeChars ?? null,
      chunkOverlapChars: settingsForm.chunkOverlapChars ?? null,
      topK: settingsForm.topK ?? null,
      systemPrompt: settingsForm.systemPrompt.trim() || null,
    })
    settingsVisible.value = false
    message.success('已保存，仅对新上传的文档与新提问生效')
  } catch (e) {
    message.error(describeError(e))
  } finally {
    settingsSaving.value = false
  }
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
 * 打开成员管理。
 * @param {object} kb 知识库
 */
async function openMembers(kb) {
  activeKb.value = kb
  memberForm.userId = ''
  memberForm.role = 1
  membersVisible.value = true
  await loadMembers()
}

/** 拉取当前知识库成员。 */
async function loadMembers() {
  if (!activeKb.value) return
  membersLoading.value = true
  try {
    members.value = await knowledgeBaseMemberApi.list(activeKb.value.id)
  } catch (e) {
    message.error(describeError(e))
  } finally {
    membersLoading.value = false
  }
}

/** 新增成员或调整角色。 */
async function saveMember() {
  if (!activeKb.value) return
  memberSaving.value = true
  try {
    await knowledgeBaseMemberApi.set({
      knowledgeBaseId: activeKb.value.id,
      userId: memberForm.userId.trim(),
      role: memberForm.role,
    })
    memberForm.userId = ''
    await loadMembers()
    message.success('已保存')
  } catch (e) {
    message.error(describeError(e))
  } finally {
    memberSaving.value = false
  }
}

/**
 * 移除成员。
 * @param {object} member 成员
 */
async function removeMember(member) {
  if (!activeKb.value) return
  try {
    await knowledgeBaseMemberApi.remove(activeKb.value.id, member.userId)
    await loadMembers()
    message.success('已移除')
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
.kb-panel {
  max-width: 1080px;
  margin: 0 auto;
  display: flex;
  flex-direction: column;
  gap: 20px;
}

.kb-header {
  display: flex;
  align-items: flex-end;
  justify-content: space-between;
  gap: 16px;
}

.kb-eyebrow {
  font-size: 10.5px;
  font-weight: 700;
  letter-spacing: 0.16em;
  text-transform: uppercase;
  color: var(--kv-primary);
}

.kb-title {
  margin: 4px 0 0;
  font-size: 24px;
  font-weight: 700;
  letter-spacing: -0.02em;
  color: var(--kv-text);
}

.kb-subtitle {
  margin: 6px 0 0;
  font-size: 13px;
  color: var(--kv-text-3);
}

.kb-create {
  flex: none;
  height: 38px;
  border-radius: 10px;
}

.kb-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(300px, 1fr));
  gap: 14px;
}

.kb-card {
  display: flex;
  flex-direction: column;
  gap: 10px;
  padding: 18px;
  border: 1px solid var(--kv-border);
  border-radius: 16px;
  background: var(--kv-surface);
  box-shadow: var(--kv-shadow-sm);
  transition: transform 0.2s ease, box-shadow 0.2s ease, border-color 0.2s ease;
}

.kb-card:hover {
  transform: translateY(-2px);
  border-color: var(--kv-border-strong);
  box-shadow: var(--kv-shadow-md);
}

.kb-card-head {
  display: flex;
  align-items: center;
  gap: 10px;
}

.kb-icon {
  width: 36px;
  height: 36px;
  flex: none;
  display: grid;
  place-items: center;
  border: 1px solid #d9eae5;
  border-radius: 10px;
  color: var(--kv-primary);
  background: var(--kv-primary-soft);
}

.kb-name-row {
  min-width: 0;
  display: flex;
  align-items: center;
  gap: 8px;
}

.kb-name-row h3 {
  margin: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  font-size: 15px;
  font-weight: 650;
  letter-spacing: -0.01em;
  color: var(--kv-text);
}

.kb-desc {
  min-height: 40px;
  margin: 0;
  display: -webkit-box;
  overflow: hidden;
  -webkit-line-clamp: 2;
  -webkit-box-orient: vertical;
  font-size: 12.5px;
  line-height: 1.65;
  color: var(--kv-text-2);
}

.kb-card-meta {
  display: flex;
  align-items: center;
  gap: 8px;
}

.meta-item {
  display: inline-flex;
  align-items: center;
  gap: 5px;
  font-size: 12px;
  color: var(--kv-text-3);
}

.meta-item strong {
  font-weight: 650;
  color: var(--kv-text);
}

.kb-card-actions {
  display: flex;
  align-items: center;
  gap: 2px;
  margin-top: auto;
  padding-top: 10px;
  border-top: 1px solid var(--kv-border);
}

.spacer {
  flex: 1;
}

.kb-empty {
  min-height: 200px;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 6px;
  border: 1px dashed var(--kv-border-strong);
  border-radius: 16px;
  color: var(--kv-text-3);
}

.kb-empty p {
  margin: 0;
  font-size: 13px;
  font-weight: 600;
  color: var(--kv-text-2);
}

.kb-empty span {
  font-size: 11px;
}

.settings-tip {
  margin-bottom: 14px;
}

.kb-form-footer {
  display: flex;
  justify-content: flex-end;
  gap: 8px;
}

.member-list {
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.member-row {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 8px 10px;
  border: 1px solid var(--kv-border);
  border-radius: 10px;
  background: var(--kv-surface-soft);
}

.member-avatar {
  width: 28px;
  height: 28px;
  flex: none;
  display: grid;
  place-items: center;
  border-radius: 8px;
  background: var(--kv-primary-soft);
  color: var(--kv-primary);
  font-size: 12px;
  font-weight: 700;
}

.member-name {
  flex: 1;
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  font-size: 13px;
  font-weight: 500;
}

.member-empty {
  padding: 22px 12px;
  text-align: center;
  font-size: 12px;
  color: var(--kv-text-3);
}

.kv-modal :deep(.n-card-header) {
  padding-bottom: 6px;
}

@media (max-width: 640px) {
  .kb-header {
    align-items: flex-start;
    flex-direction: column;
  }

  .kb-title {
    font-size: 21px;
  }

  .kb-grid {
    grid-template-columns: 1fr;
  }
}
</style>

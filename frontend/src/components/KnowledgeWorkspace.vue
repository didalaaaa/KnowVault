<template>
  <div class="kb-workspace">
    <aside class="kb-list-pane" aria-label="知识库">
      <div class="list-head">
        <div>
          <span class="kb-eyebrow">Workspace</span>
          <h2 class="list-title">知识库</h2>
        </div>
        <n-button type="primary" size="small" @click="openCreate">
          <template #icon><KvIcon name="plus" :size="14" /></template>
          新建
        </n-button>
      </div>

      <div class="kb-list" role="listbox" aria-label="选择知识库">
        <button
          v-for="kb in documents.knowledgeBases"
          :key="kb.id"
          type="button"
          class="kb-item"
          :class="{ active: kb.id === documents.knowledgeBaseId }"
          role="option"
          :aria-selected="kb.id === documents.knowledgeBaseId ? 'true' : 'false'"
          @click="select(kb)"
        >
          <span class="kb-item-icon"><KvIcon name="layers" :size="17" :stroke-width="1.7" /></span>
          <span class="kb-item-main">
            <span class="kb-item-name">
              <span class="name-text" :title="kb.name">{{ kb.name }}</span>
              <span v-if="kb.id === defaultKnowledgeBaseId" class="default-badge">默认</span>
            </span>
            <span class="kb-item-desc">{{ kb.description || '未填写描述' }}</span>
            <span class="kb-item-meta">已就绪 {{ kb.readyDocumentCount }} 篇</span>
          </span>
        </button>

        <div v-if="documents.knowledgeBases.length === 0" class="kb-list-empty">
          <KvIcon name="book-open" :size="20" :stroke-width="1.6" />
          <p>还没有知识库</p>
          <span>新建一个，开始整理你的资料</span>
        </div>
      </div>
    </aside>

    <section class="kb-detail" aria-label="知识库详情">
      <template v-if="activeKb">
        <header class="kb-hero">
          <div class="kb-hero-main">
            <div class="kb-hero-title">
              <h2 :title="activeKb.name">{{ activeKb.name }}</h2>
              <n-tag v-if="isDefault" size="small" type="success" :bordered="false">默认</n-tag>
            </div>
            <p class="kb-hero-desc">{{ activeKb.description || '未填写描述' }}</p>
            <div class="kb-facts">
              <span class="kb-fact"><strong>{{ documents.documents.length }}</strong> 篇文档</span>
              <span class="kb-fact"><strong>{{ readyCount }}</strong> 篇已就绪</span>
            </div>
          </div>
          <div class="kb-hero-actions">
            <n-button size="small" secondary @click="openSettings(activeKb)">
              <template #icon><KvIcon name="settings" :size="14" /></template>
              设置
            </n-button>
            <n-button size="small" secondary :disabled="isDefault" @click="openMembers(activeKb)">
              <template #icon><KvIcon name="users" :size="14" /></template>
              成员
            </n-button>
            <n-button size="small" secondary @click="openEdit(activeKb)">
              <template #icon><KvIcon name="pencil" :size="14" /></template>
              编辑
            </n-button>
            <n-popconfirm @positive-click="remove(activeKb)">
              <template #trigger>
                <n-button size="small" quaternary type="error" :disabled="isDefault">
                  <template #icon><KvIcon name="trash" :size="14" /></template>
                  删除
                </n-button>
              </template>
              删除后不可恢复；知识库下还有文档时后端会拒绝。确认删除「{{ activeKb.name }}」？
            </n-popconfirm>
          </div>
        </header>

        <KnowledgeDocuments class="kb-docs" />
      </template>

      <div v-else class="kb-detail-empty">
        <KvIcon name="layers" :size="24" :stroke-width="1.6" />
        <p>选择或新建一个知识库</p>
        <span>文档、上传与权限都归属于知识库</span>
      </div>
    </section>

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
import { computed, reactive, ref } from 'vue'
import {
  NAlert, NButton, NDivider, NForm, NFormItem, NInput, NInputNumber, NModal,
  NPopconfirm, NSelect, NTag, useMessage,
} from 'naive-ui'
import KvIcon from './KvIcon.vue'
import KnowledgeDocuments from './KnowledgeDocuments.vue'
import { useDocumentStore } from '../stores/documents'
import { knowledgeBaseMemberApi } from '../api/knowledgeBaseMembers'

const documents = useDocumentStore()
const message = useMessage()

// 与后端 KnowVaultConsts.DefaultKnowledgeBaseId 保持一致；默认库禁止删除与改成员
const defaultKnowledgeBaseId = '00000000-0000-0000-0000-000000000001'

const activeKb = computed(
  () => documents.knowledgeBases.find((kb) => kb.id === documents.knowledgeBaseId) || null,
)
const isDefault = computed(() => activeKb.value?.id === defaultKnowledgeBaseId)
const readyCount = computed(() => documents.documents.filter((doc) => doc.status === 2).length)

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
const memberForm = reactive({ userId: '', role: 1 })

const roleOptions = [
  { label: 'Viewer（只读 + 提问）', value: 1 },
  { label: 'Editor（上传 / 删除 / 重处理）', value: 2 },
  { label: 'Owner（管成员 / 删库）', value: 3 },
]

const roleText = (role) => ({ 1: 'Viewer', 2: 'Editor', 3: 'Owner' })[role] ?? '未知'

/** 取用户名首字符作为头像占位 */
const initial = (name) => (name || '?').trim().charAt(0).toUpperCase()

/**
 * 切换当前知识库：文档列表、知识库计数与会话侧栏都跟随这个选择
 * @param {object} kb 知识库
 */
function select(kb) {
  if (kb.id === documents.knowledgeBaseId) return
  documents.knowledgeBaseId = kb.id
  documents.refresh(kb.id)
}

/** 打开「新建」表单 */
function openCreate() {
  editingId.value = ''
  form.name = ''
  form.description = ''
  formVisible.value = true
}

/**
 * 打开「编辑」表单
 * @param {object} kb 知识库
 */
function openEdit(kb) {
  editingId.value = kb.id
  form.name = kb.name
  form.description = kb.description || ''
  formVisible.value = true
}

/**
 * 打开「设置」弹窗，用知识库当前值预填
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

/** 保存切片与检索设置；留空的字段传 null 由后端回落全局配置 */
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

/** 保存（新建或编辑）；失败时弹后端的中文文案 */
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
 * 删除知识库（非空库与默认库由后端拒绝）
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
 * 打开成员管理
 * @param {object} kb 知识库
 */
async function openMembers(kb) {
  memberForm.userId = ''
  memberForm.role = 1
  membersVisible.value = true
  await loadMembers()
}

/** 拉取当前知识库成员 */
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

/** 新增成员或调整角色 */
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
 * 移除成员
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
 * 把未知错误整理成一行可读文案（ABP 的 UserFriendlyException 文案在 error.message）
 * @param {any} e 错误对象
 * @returns {string} 错误文案
 */
function describeError(e) {
  return e?.response?.data?.error?.message || e?.message || '操作失败'
}
</script>

<style scoped>
.kb-workspace {
  height: 100%;
  min-height: 0;
  display: grid;
  grid-template-columns: 318px minmax(0, 1fr);
  gap: 18px;
}

.kb-list-pane {
  min-height: 0;
  display: flex;
  flex-direction: column;
  gap: 14px;
  overflow: hidden;
  padding: 18px 14px 16px 18px;
  border: 1px solid var(--kv-border);
  border-radius: var(--kv-radius-lg);
  background: var(--kv-surface);
  box-shadow: var(--kv-shadow-md);
}

.list-head {
  flex: none;
  display: flex;
  align-items: flex-end;
  justify-content: space-between;
  gap: 10px;
  padding-right: 4px;
}

.kb-eyebrow {
  font-size: 10px;
  font-weight: 700;
  letter-spacing: 0.16em;
  text-transform: uppercase;
  color: var(--kv-primary);
}

.list-title {
  margin: 3px 0 0;
  font-size: 20px;
  font-weight: 700;
  letter-spacing: -0.02em;
  color: var(--kv-text);
}

.kb-list {
  flex: 1;
  min-height: 0;
  overflow-y: auto;
  display: flex;
  flex-direction: column;
  gap: 5px;
  margin-right: -6px;
  padding-right: 6px;
}

.kb-item {
  width: 100%;
  display: flex;
  align-items: flex-start;
  gap: 10px;
  padding: 11px 10px;
  border: 1px solid transparent;
  border-radius: 12px;
  background: transparent;
  font-family: inherit;
  text-align: left;
  cursor: pointer;
  transition: background-color 0.16s ease, border-color 0.16s ease;
}

.kb-item:hover {
  background: var(--kv-surface-soft);
}

.kb-item:focus-visible {
  outline: 2px solid var(--kv-primary);
  outline-offset: 1px;
}

.kb-item.active {
  border-color: #cfe4de;
  background: var(--kv-primary-soft);
}

.kb-item-icon {
  width: 34px;
  height: 34px;
  flex: none;
  display: grid;
  place-items: center;
  border: 1px solid var(--kv-border);
  border-radius: 10px;
  background: #ffffff;
  color: var(--kv-primary);
}

.kb-item.active .kb-item-icon {
  border-color: #cfe4de;
}

.kb-item-main {
  min-width: 0;
  flex: 1;
  display: flex;
  flex-direction: column;
  gap: 2px;
}

.kb-item-name {
  min-width: 0;
  display: flex;
  align-items: center;
  gap: 6px;
}

.name-text {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  font-size: 13.5px;
  font-weight: 600;
  color: var(--kv-text);
}

.kb-item.active .name-text {
  color: var(--kv-primary-strong);
}

.default-badge {
  flex: none;
  padding: 0 6px;
  border-radius: 999px;
  background: #eef7f2;
  color: var(--kv-success);
  font-size: 10px;
  font-weight: 600;
  line-height: 17px;
}

.kb-item-desc {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  font-size: 11.5px;
  color: var(--kv-text-3);
}

.kb-item-meta {
  font-size: 11px;
  color: var(--kv-text-3);
  font-variant-numeric: tabular-nums;
}

.kb-list-empty {
  min-height: 160px;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 5px;
  padding: 20px 12px;
  border: 1px dashed var(--kv-border-strong);
  border-radius: 12px;
  text-align: center;
  color: var(--kv-text-3);
}

.kb-list-empty p {
  margin: 0;
  font-size: 12.5px;
  font-weight: 600;
  color: var(--kv-text-2);
}

.kb-list-empty span {
  font-size: 11px;
}

.kb-detail {
  min-width: 0;
  min-height: 0;
  display: flex;
  flex-direction: column;
  overflow: hidden;
  border: 1px solid var(--kv-border);
  border-radius: var(--kv-radius-lg);
  background: var(--kv-surface);
  box-shadow: var(--kv-shadow-md);
}

.kb-hero {
  flex: none;
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 20px;
  padding: 22px 24px 18px;
  border-bottom: 1px solid var(--kv-border);
  background: linear-gradient(180deg, #fbfcf9, #ffffff 88%);
}

.kb-hero-main {
  min-width: 0;
}

.kb-hero-title {
  min-width: 0;
  display: flex;
  align-items: center;
  gap: 8px;
}

.kb-hero-title h2 {
  margin: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  font-size: 20px;
  font-weight: 700;
  letter-spacing: -0.02em;
  color: var(--kv-text);
}

.kb-hero-desc {
  margin: 6px 0 0;
  max-width: 620px;
  font-size: 12.5px;
  line-height: 1.6;
  color: var(--kv-text-3);
}

.kb-facts {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
  margin-top: 12px;
}

.kb-fact {
  padding: 3px 9px;
  border: 1px solid var(--kv-border);
  border-radius: 999px;
  background: #ffffff;
  font-size: 11.5px;
  color: var(--kv-text-3);
  font-variant-numeric: tabular-nums;
}

.kb-fact strong {
  font-weight: 650;
  color: var(--kv-text);
}

.kb-hero-actions {
  flex: none;
  display: flex;
  flex-wrap: wrap;
  justify-content: flex-end;
  gap: 4px;
  max-width: 330px;
}

.kb-docs {
  flex: 1;
  min-height: 0;
}

.kb-detail-empty {
  flex: 1;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 6px;
  padding: 40px 16px;
  text-align: center;
  color: var(--kv-text-3);
}

.kb-detail-empty p {
  margin: 2px 0 0;
  font-size: 13px;
  font-weight: 600;
  color: var(--kv-text-2);
}

.kb-detail-empty span {
  font-size: 11.5px;
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

@media (max-width: 1100px) {
  .kb-workspace {
    grid-template-columns: 282px minmax(0, 1fr);
  }
}

@media (max-width: 900px) {
  .kb-workspace {
    height: auto;
    grid-template-columns: 1fr;
  }

  .kb-list-pane {
    overflow: visible;
    padding: 16px;
  }

  .kb-list {
    flex-direction: row;
    overflow-x: auto;
    overflow-y: visible;
    margin-right: 0;
    padding: 2px 2px 6px;
  }

  .kb-item {
    min-width: 230px;
    max-width: 260px;
    background: var(--kv-surface-soft);
  }

  .kb-item.active {
    background: var(--kv-primary-soft);
  }

  .kb-detail {
    min-height: 62vh;
  }

  .kb-hero {
    flex-direction: column;
    gap: 14px;
  }

  .kb-hero-actions {
    justify-content: flex-start;
    max-width: none;
  }

  .kb-docs {
    flex: none;
  }
}

@media (max-width: 640px) {
  .kb-hero {
    padding: 18px 16px 14px;
  }

  .kb-hero-title h2 {
    font-size: 18px;
  }
}
</style>

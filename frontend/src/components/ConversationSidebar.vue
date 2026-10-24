<template>
  <div class="conv-panel">
    <div class="panel-head">
      <div>
        <h2>会话</h2>
        <p>刷新页面后仍可继续上次的问答</p>
      </div>
      <span class="conv-count">{{ conversations.items.length }}</span>
    </div>

    <n-alert v-if="conversations.error" type="error" :show-icon="false" closable @close="conversations.error = ''">
      {{ conversations.error }}
    </n-alert>

    <n-spin v-if="conversations.loading && conversations.items.length === 0" :size="18" class="conv-loading" />

    <div v-else class="conv-list">
      <article
        v-for="item in conversations.items"
        :key="item.id"
        class="conv-item"
        :class="{ active: chat.conversationId === item.id }"
        tabindex="0"
        role="button"
        :aria-current="chat.conversationId === item.id ? 'true' : undefined"
        @click="open(item)"
        @keydown.enter.prevent="open(item)"
        @dblclick="startRename(item)"
      >
        <span class="conv-main">
          <span class="conv-title" :title="item.title">{{ item.title }}</span>
          <span class="conv-time">{{ formatTime(item.lastMessageAt || item.creationTime) }}</span>
        </span>
        <span class="conv-actions">
          <n-button size="tiny" quaternary aria-label="重命名会话" @click.stop="startRename(item)">
            <template #icon><KvIcon name="pencil" :size="13" /></template>
          </n-button>
          <n-popconfirm @positive-click="remove(item)">
            <template #trigger>
              <n-button size="tiny" quaternary type="error" aria-label="删除会话" @click.stop>
                <template #icon><KvIcon name="trash" :size="13" /></template>
              </n-button>
            </template>
            删除后该会话的消息与引用不可恢复。确认删除「{{ item.title }}」？
          </n-popconfirm>
        </span>
      </article>

      <div v-if="conversations.items.length === 0" class="conv-empty">
        <KvIcon name="layers" :size="22" :stroke-width="1.6" />
        <p>还没有会话</p>
        <span>在右侧提一个问题，会话会自动保存在这里</span>
      </div>
    </div>

    <n-modal
      v-model:show="renameVisible"
      preset="card"
      title="重命名会话"
      style="width: min(420px, calc(100vw - 32px))"
      @after-leave="renameTitle = ''"
    >
      <n-input v-model:value="renameTitle" maxlength="256" show-count placeholder="给这段对话起个名字" @keydown.enter.prevent="confirmRename" />
      <template #footer>
        <div class="modal-actions">
          <n-button @click="renameVisible = false">取消</n-button>
          <n-button type="primary" :loading="renaming" :disabled="!renameTitle.trim()" @click="confirmRename">保存</n-button>
        </div>
      </template>
    </n-modal>
  </div>
</template>

<script setup>
import { ref, watch } from 'vue'
import { NAlert, NButton, NInput, NModal, NPopconfirm, NSpin, useMessage } from 'naive-ui'
import KvIcon from './KvIcon.vue'
import { useChatStore } from '../stores/chat'
import { useConversationStore } from '../stores/conversations'
import { useDocumentStore } from '../stores/documents'

const chat = useChatStore()
const conversations = useConversationStore()
const documents = useDocumentStore()
const message = useMessage()

const renameVisible = ref(false)
const renameTitle = ref('')
const renaming = ref(false)
let renameTarget = null

// 切换知识库或首次加载时刷新会话列表
watch(
  () => documents.knowledgeBaseId,
  (knowledgeBaseId) => conversations.refresh(knowledgeBaseId || undefined),
  { immediate: true },
)

/**
 * 打开历史会话：拉取消息并恢复引用。
 * @param {object} item 会话摘要
 */
function open(item) {
  if (chat.conversationId === item.id) return
  chat.loadConversation(item.id)
}

/**
 * 打开重命名弹窗。
 * @param {object} item 会话摘要
 */
function startRename(item) {
  renameTarget = item
  renameTitle.value = item.title
  renameVisible.value = true
}

/** 保存重命名结果。 */
async function confirmRename() {
  const title = renameTitle.value.trim()
  if (!title || !renameTarget) return

  renaming.value = true
  try {
    await conversations.rename(renameTarget.id, title)
    renameVisible.value = false
    message.success('已重命名')
  } catch (e) {
    message.error(describeError(e))
  } finally {
    renaming.value = false
  }
}

/**
 * 删除会话；删除的是当前会话时回到空白状态。
 * @param {object} item 会话摘要
 */
async function remove(item) {
  try {
    await conversations.remove(item.id)
    if (chat.conversationId === item.id) chat.reset()
    message.success('已删除')
  } catch (e) {
    message.error(describeError(e))
  }
}

/**
 * 列表时间：今天显示时分，今年显示月日，更早显示完整日期。
 * @param {string} value ISO 时间
 * @returns {string} 展示文本
 */
function formatTime(value) {
  if (!value) return ''
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return ''

  const now = new Date()
  const sameDay = date.toDateString() === now.toDateString()
  if (sameDay) return date.toLocaleTimeString('zh-CN', { hour: '2-digit', minute: '2-digit' })
  if (date.getFullYear() === now.getFullYear()) {
    return `${String(date.getMonth() + 1).padStart(2, '0')}-${String(date.getDate()).padStart(2, '0')}`
  }
  return date.toLocaleDateString('zh-CN')
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
.conv-panel {
  height: 100%;
  display: flex;
  flex-direction: column;
  gap: 12px;
  overflow: hidden;
  padding: 20px 14px 16px 18px;
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

.conv-count {
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

.conv-loading {
  display: grid;
  place-items: center;
  padding: 28px 0;
}

.conv-list {
  flex: 1;
  min-height: 0;
  overflow-y: auto;
  display: flex;
  flex-direction: column;
  gap: 6px;
  padding-right: 4px;
}

.conv-item {
  position: relative;
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 10px 8px 10px 12px;
  border: 1px solid transparent;
  border-radius: 12px;
  cursor: pointer;
  transition: background-color 0.16s ease, border-color 0.16s ease;
}

.conv-item:hover {
  background: var(--kv-surface-soft);
}

.conv-item:focus-visible {
  outline: 2px solid var(--kv-primary);
  outline-offset: 1px;
}

.conv-item.active {
  border-color: #cfe4de;
  background: var(--kv-primary-soft);
}

.conv-item.active::before {
  content: "";
  position: absolute;
  left: 4px;
  top: 11px;
  bottom: 11px;
  width: 3px;
  border-radius: 3px;
  background: var(--kv-primary);
}

.conv-main {
  min-width: 0;
  flex: 1;
  display: flex;
  flex-direction: column;
  gap: 3px;
}

.conv-title {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  font-size: 13px;
  font-weight: 500;
  color: var(--kv-text);
}

.conv-item.active .conv-title {
  color: var(--kv-primary-strong);
  font-weight: 600;
}

.conv-time {
  font-size: 11px;
  color: var(--kv-text-3);
}

.conv-actions {
  flex: none;
  display: flex;
  align-items: center;
  gap: 2px;
  opacity: 0;
  transition: opacity 0.16s ease;
}

.conv-item:hover .conv-actions,
.conv-item:focus-within .conv-actions,
.conv-item.active .conv-actions {
  opacity: 1;
}

.conv-empty {
  flex: 1;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 6px;
  padding: 32px 16px;
  color: var(--kv-text-3);
  text-align: center;
}

.conv-empty p {
  margin: 2px 0 0;
  font-size: 13px;
  font-weight: 600;
  color: var(--kv-text-2);
}

.conv-empty span {
  font-size: 11.5px;
  line-height: 1.6;
}

.modal-actions {
  display: flex;
  justify-content: flex-end;
  gap: 10px;
}

@media (hover: none) {
  .conv-actions {
    opacity: 1;
  }
}
</style>

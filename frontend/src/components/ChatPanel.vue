<template>
  <div class="chat-panel">
    <div class="messages" ref="listRef" aria-label="对话消息">
      <div v-if="chat.messages.length === 0" class="empty">
        <span class="empty-icon"><KvIcon name="sparkles" :size="26" :stroke-width="1.6" /></span>
        <h2>从你的资料里找答案</h2>
        <p>左侧上传文档，等状态变为「已就绪」后提问：它会按你的资料作答并标注出处。</p>
        <div class="empty-hints">
          <span>语义检索</span>
          <span>引用溯源</span>
          <span>私有资料</span>
        </div>
      </div>

      <div v-for="(m, i) in chat.messages" :key="i" class="msg" :class="'msg-' + m.role">
        <div class="avatar" :class="'avatar-' + m.role">
          <KvIcon :name="m.role === 'assistant' ? 'sparkles' : 'user'" :size="15" />
        </div>
        <div class="msg-body">
          <div class="msg-meta">{{ m.role === 'assistant' ? 'KnowVault' : '你' }}</div>
          <div class="bubble">
            <p v-if="m.content" class="msg-text">{{ m.content }}</p>
            <p v-else class="pending">
              <span class="typing"><i></i><i></i><i></i></span>
              正在生成…
            </p>
            <div v-if="m.citations && m.citations.length" class="citations">
              <div class="cite-head">
                <KvIcon name="quote" :size="12" />
                引用来源
                <span>{{ m.citations.length }}</span>
              </div>
              <div class="cite-list">
                <button
                  v-for="(c, j) in m.citations"
                  :key="j"
                  type="button"
                  class="cite-chip"
                  @click="openCitation(c)"
                >
                  <span class="cite-index">{{ j + 1 }}</span>
                  <span class="cite-name" :title="c.title">{{ c.title }}</span>
                  <span class="cite-meta">片段 {{ c.chunkIndex }} · {{ c.score.toFixed(3) }}</span>
                </button>
              </div>
            </div>
          </div>
        </div>
      </div>

      <div v-if="chat.loading && !chat.streaming" class="msg msg-assistant">
        <div class="avatar avatar-assistant"><KvIcon name="sparkles" :size="15" /></div>
        <div class="msg-body">
          <div class="msg-meta">KnowVault</div>
          <div class="bubble">
            <span class="pending">
              <span class="typing"><i></i><i></i><i></i></span>
              正在检索资料并生成回答…
            </span>
          </div>
        </div>
      </div>
    </div>

    <div class="composer">
      <div class="composer-box">
        <n-input
          v-model:value="question"
          class="composer-input"
          placeholder="输入你的问题…"
          aria-label="输入你的问题"
          :disabled="chat.loading"
          @keydown.enter.prevent="send"
        />
        <n-button v-if="chat.streaming" type="warning" class="send-btn" @click="stop">
          <template #icon><KvIcon name="stop" :size="14" /></template>
          停止生成
        </n-button>
        <n-button
          v-else
          type="primary"
          class="send-btn"
          :loading="chat.loading"
          :disabled="!question.trim()"
          @click="send"
        >
          <template #icon><KvIcon name="send" :size="14" /></template>
          发送
        </n-button>
      </div>
      <div class="composer-hint">Enter 发送 · 回答仅基于你上传的资料</div>
    </div>

    <CitationDrawer v-model:show="citationVisible" :citation="activeCitation" @open-document="openCitationDocument" />
    <DocumentDetailDrawer v-model:show="detailVisible" :document="detailDoc" />
  </div>
</template>

<script setup>
import { ref, nextTick, watch } from 'vue'
import { NInput, NButton, useMessage } from 'naive-ui'
import KvIcon from './KvIcon.vue'
import CitationDrawer from './CitationDrawer.vue'
import DocumentDetailDrawer from './DocumentDetailDrawer.vue'
import { useChatStore } from '../stores/chat'
import { useConversationStore } from '../stores/conversations'
import { useDocumentStore } from '../stores/documents'

const chat = useChatStore()
const conversations = useConversationStore()
const documents = useDocumentStore()
const message = useMessage()
const question = ref('')
const listRef = ref(null)
const citationVisible = ref(false)
const activeCitation = ref(null)
const detailVisible = ref(false)
const detailDoc = ref(null)

/** 滚到底部，让最新内容始终可见。 */
async function scrollToBottom() {
  await nextTick()
  listRef.value?.scrollTo({ top: listRef.value.scrollHeight })
}

// 流式期间正文持续增长（不是整体替换），必须跟着增量滚，否则回答会滚出视野
watch(
  () => chat.messages[chat.messages.length - 1]?.content,
  () => scrollToBottom(),
)

// 发送问题：带上当前选中的知识库（检索按 knowledgeBaseId 过滤）
async function send() {
  const q = question.value.trim()
  if (!q || chat.loading) return
  question.value = ''
  await chat.ask(q, documents.knowledgeBaseId)
  // 新一轮问答会新建会话或刷新活动时间：同步侧栏列表的顺序与标题
  if (chat.conversationId) conversations.refresh(documents.knowledgeBaseId)
  await scrollToBottom()
}

// 停止生成：取消服务端流
function stop() {
  chat.stop()
}

/**
 * 打开引用详情抽屉
 * @param {object} citation 引用
 */
function openCitation(citation) {
  activeCitation.value = citation
  citationVisible.value = true
}

/**
 * 从引用跳到文档详情：先用当前文档列表，找不到时刷新一次再找
 * @param {object} citation 引用
 */
async function openCitationDocument(citation) {
  citationVisible.value = false
  let doc = documents.documents.find((item) => item.id === citation.documentId)
  if (!doc) {
    await documents.refresh()
    doc = documents.documents.find((item) => item.id === citation.documentId)
  }
  if (!doc) {
    message.warning('文档可能已被删除')
    return
  }
  detailDoc.value = doc
  detailVisible.value = true
}
</script>

<style scoped>
.chat-panel {
  height: 100%;
  display: flex;
  flex-direction: column;
  overflow: hidden;
  border: 1px solid var(--kv-border);
  border-radius: var(--kv-radius-lg);
  background: var(--kv-surface);
  box-shadow: var(--kv-shadow-md);
}

.messages {
  flex: 1;
  min-height: 0;
  overflow-y: auto;
  display: flex;
  flex-direction: column;
  gap: 20px;
  padding: 26px 28px 14px;
}

.empty {
  max-width: 440px;
  margin: auto;
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 10px;
  text-align: center;
  color: var(--kv-text-2);
}

.empty-icon {
  width: 56px;
  height: 56px;
  display: grid;
  place-items: center;
  margin-bottom: 6px;
  border: 1px solid #d6e9e4;
  border-radius: 18px;
  color: var(--kv-primary);
  background: var(--kv-primary-soft);
  box-shadow: inset 0 1px 0 rgba(255, 255, 255, 0.7);
}

.empty h2 {
  margin: 0;
  font-size: 20px;
  font-weight: 650;
  letter-spacing: -0.01em;
  color: var(--kv-text);
}

.empty p {
  margin: 0;
  font-size: 14px;
  line-height: 1.75;
}

.empty-hints {
  display: flex;
  flex-wrap: wrap;
  justify-content: center;
  gap: 8px;
  margin-top: 12px;
}

.empty-hints span {
  padding: 4px 10px;
  border: 1px solid var(--kv-border);
  border-radius: 999px;
  background: var(--kv-surface-soft);
  font-size: 12px;
  color: var(--kv-text-2);
}

.msg {
  width: 100%;
  max-width: 820px;
  margin: 0 auto;
  display: flex;
  gap: 10px;
}

.msg-user {
  flex-direction: row-reverse;
}

.avatar {
  width: 30px;
  height: 30px;
  flex: none;
  display: grid;
  place-items: center;
  margin-top: 2px;
  border-radius: 9px;
}

.avatar-assistant {
  color: var(--kv-primary);
  background: var(--kv-primary-soft);
  border: 1px solid #d6e9e4;
}

.avatar-user {
  color: #6b5a3c;
  background: #f4efe4;
  border: 1px solid #e8dfcc;
}

.msg-body {
  min-width: 0;
  max-width: calc(100% - 48px);
  display: flex;
  flex-direction: column;
  gap: 5px;
}

.msg-user .msg-body {
  align-items: flex-end;
}

.msg-meta {
  padding: 0 2px;
  font-size: 10.5px;
  font-weight: 600;
  letter-spacing: 0.06em;
  text-transform: uppercase;
  color: var(--kv-text-3);
}

.bubble {
  max-width: 100%;
  padding: 13px 16px;
  border: 1px solid var(--kv-border);
  border-radius: 14px;
  background: var(--kv-surface-soft);
  box-shadow: var(--kv-shadow-sm);
  font-size: 14.5px;
  line-height: 1.75;
  color: var(--kv-text);
}

.msg-assistant .bubble {
  border-top-left-radius: 4px;
  background: #fbfcf9;
}

.msg-user .bubble {
  border-color: transparent;
  border-top-right-radius: 4px;
  color: #ffffff;
  background: linear-gradient(135deg, #117d74, #0d645e);
  box-shadow: 0 6px 16px rgba(15, 118, 110, 0.18);
}

.msg-text {
  margin: 0;
  white-space: pre-wrap;
  word-break: break-word;
}

.pending {
  display: inline-flex;
  align-items: center;
  gap: 8px;
  margin: 0;
  font-size: 13.5px;
  color: var(--kv-text-3);
}

.typing {
  display: inline-flex;
  gap: 3px;
}

.typing i {
  width: 5px;
  height: 5px;
  border-radius: 50%;
  background: var(--kv-primary);
  opacity: 0.3;
  animation: kv-blink 1.2s infinite ease-in-out;
}

.typing i:nth-child(2) {
  animation-delay: 0.15s;
}

.typing i:nth-child(3) {
  animation-delay: 0.3s;
}

@keyframes kv-blink {
  0%,
  80%,
  100% {
    opacity: 0.25;
    transform: translateY(0);
  }
  40% {
    opacity: 0.9;
    transform: translateY(-2px);
  }
}

.citations {
  margin-top: 12px;
  padding-top: 11px;
  border-top: 1px solid var(--kv-border);
}

.cite-head {
  display: flex;
  align-items: center;
  gap: 6px;
  margin-bottom: 8px;
  font-size: 10.5px;
  font-weight: 600;
  letter-spacing: 0.08em;
  text-transform: uppercase;
  color: var(--kv-text-3);
}

.cite-head span {
  padding: 1px 6px;
  border-radius: 999px;
  background: var(--kv-primary-soft);
  color: var(--kv-primary);
  font-size: 10px;
  letter-spacing: 0;
}

.cite-list {
  display: flex;
  flex-wrap: wrap;
  gap: 6px;
}

.cite-chip {
  max-width: 100%;
  display: inline-flex;
  align-items: center;
  gap: 7px;
  padding: 5px 9px;
  border: 1px solid var(--kv-border);
  border-radius: 8px;
  background: #ffffff;
  font-family: inherit;
  font-size: 12px;
  text-align: left;
  color: var(--kv-text-2);
  cursor: pointer;
  transition: border-color 0.18s ease, background 0.18s ease;
}

.cite-chip:hover {
  border-color: var(--kv-primary);
  background: var(--kv-primary-soft);
}

.cite-chip:focus-visible {
  outline: 2px solid var(--kv-primary);
  outline-offset: 2px;
}

.cite-index {
  width: 18px;
  height: 18px;
  flex: none;
  display: grid;
  place-items: center;
  border-radius: 5px;
  background: var(--kv-accent-soft);
  color: var(--kv-accent);
  font-size: 10px;
  font-weight: 700;
}

.cite-name {
  max-width: 180px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  font-weight: 500;
  color: var(--kv-text);
}

.cite-meta {
  flex: none;
  font-size: 11px;
  color: var(--kv-text-3);
  font-variant-numeric: tabular-nums;
}

.composer {
  flex: none;
  padding: 12px 22px 16px;
  border-top: 1px solid var(--kv-border);
  background: linear-gradient(180deg, rgba(251, 252, 249, 0), #fbfcf9 32%);
}

.composer-box {
  max-width: 820px;
  margin: 0 auto;
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 8px 8px 8px 14px;
  border: 1px solid var(--kv-border-strong);
  border-radius: 14px;
  background: #ffffff;
  box-shadow: var(--kv-shadow-sm);
  transition: border-color 0.18s ease, box-shadow 0.18s ease;
}

.composer-box:focus-within {
  border-color: var(--kv-primary);
  box-shadow: 0 0 0 3px rgba(15, 118, 110, 0.12);
}

.composer-input {
  flex: 1;
  --n-height: 38px;
}

.composer-input :deep(.n-input__border),
.composer-input :deep(.n-input__state-border) {
  display: none;
}

.composer-input :deep(.n-input-wrapper) {
  padding: 0;
}

.send-btn {
  flex: none;
  height: 38px;
  border-radius: 10px;
  padding: 0 16px;
}

.composer-hint {
  max-width: 820px;
  margin: 8px auto 0;
  font-size: 11px;
  text-align: center;
  color: var(--kv-text-3);
}

@media (max-width: 640px) {
  .messages {
    padding: 18px 16px 10px;
  }

  .composer {
    padding: 10px 14px 12px;
  }

  .cite-name {
    max-width: 110px;
  }
}
</style>

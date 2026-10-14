<template>
  <div class="chat-panel">
    <div class="messages" ref="listRef">
      <p v-if="chat.messages.length === 0" class="empty">
        左侧上传文档，等状态变为「已就绪」后提问：它会按你的资料作答并标注出处。
      </p>
      <div v-for="(m, i) in chat.messages" :key="i" class="msg" :class="'msg-' + m.role">
        <n-card size="small" :bordered="false" class="bubble">
          <p v-if="m.content" style="white-space: pre-wrap">{{ m.content }}</p>
          <p v-else class="pending">正在生成…</p>
          <template v-if="m.citations && m.citations.length">
            <div class="citations">
              <span class="cite-title">引用来源</span>
              <n-tag
                v-for="(c, j) in m.citations"
                :key="j"
                size="small"
                type="info"
                style="margin: 0 6px 6px 0"
              >
                {{ c.title }} · 片段 {{ c.chunkIndex }} · 相关性 {{ c.score.toFixed(3) }}
              </n-tag>
            </div>
          </template>
        </n-card>
      </div>
      <div v-if="chat.loading && !chat.streaming" class="msg msg-assistant">
        <n-card size="small" :bordered="false" class="bubble">正在检索资料并生成回答…</n-card>
      </div>
    </div>

    <div class="input-row">
      <n-input
        v-model:value="question"
        placeholder="输入你的问题…"
        :disabled="chat.loading"
        @keydown.enter.prevent="send"
      />
      <n-button v-if="chat.streaming" type="warning" @click="stop">停止生成</n-button>
      <n-button v-else type="primary" :loading="chat.loading" :disabled="!question.trim()" @click="send">
        发送
      </n-button>
    </div>
  </div>
</template>

<script setup>
import { ref, nextTick, watch } from 'vue'
import { NCard, NInput, NButton, NTag } from 'naive-ui'
import { useChatStore } from '../stores/chat'
import { useDocumentStore } from '../stores/documents'

const chat = useChatStore()
const documents = useDocumentStore()
const question = ref('')
const listRef = ref(null)

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
  await scrollToBottom()
}

// 停止生成：取消服务端流
function stop() {
  chat.stop()
}
</script>

<style scoped>
.chat-panel {
  max-width: 860px;
  margin: 0 auto;
  display: flex;
  flex-direction: column;
  gap: 16px;
  height: 100%;
}
.messages {
  flex: 1;
  overflow-y: auto;
  display: flex;
  flex-direction: column;
  gap: 12px;
  padding: 4px 0;
}
.empty { color: #999; text-align: center; margin-top: 40px; }
.msg { display: flex; }
.msg-user { justify-content: flex-end; }
.msg-assistant { justify-content: flex-start; }
.bubble { max-width: 72%; }
.msg-user .bubble { background: #e8f2ff; }
.pending { color: #999; }
.citations { margin-top: 8px; border-top: 1px dashed #eee; padding-top: 8px; }
.cite-title { font-size: 12px; color: #888; margin-right: 8px; }
.input-row { display: flex; gap: 8px; }
</style>

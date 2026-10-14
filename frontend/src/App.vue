<template>
  <n-config-provider :theme-overrides="themeOverrides" :locale="zhCN" :date-locale="dateZhCN">
    <n-message-provider>
      <LoginPanel v-if="!auth.isAuthenticated" />
      <n-layout v-else style="height: 100vh">
        <n-layout-header bordered style="padding: 16px 24px; display: flex; align-items: center; gap: 12px">
          <span style="font-weight: 700; font-size: 18px">KnowVault · 智库</span>
          <n-tag :type="chat.connected ? 'success' : 'default'" size="small" round>
            后端 {{ chat.connected ? '已连接' : '未连接' }}
          </n-tag>
          <div style="margin-left: auto; display: flex; gap: 8px">
            <n-button size="small" quaternary @click="chat.reset()">新会话</n-button>
            <n-button size="small" quaternary @click="logout">退出</n-button>
          </div>
        </n-layout-header>
        <n-layout-content content-style="padding: 24px" style="height: calc(100vh - 65px)">
          <n-tabs v-model:value="activeTab" type="line" animated class="app-tabs">
            <n-tab-pane name="qa" tab="问答" display-directive="show:lazy" style="height: 100%">
              <div class="layout">
                <aside class="side"><DocumentPanel /></aside>
                <main class="main"><ChatPanel /></main>
              </div>
            </n-tab-pane>
            <n-tab-pane name="kb" tab="知识库" display-directive="show:lazy" style="height: 100%; overflow-y: auto">
              <KnowledgeBasePanel />
            </n-tab-pane>
          </n-tabs>
        </n-layout-content>
      </n-layout>
    </n-message-provider>
  </n-config-provider>
</template>

<script setup>
import { ref, onMounted, onUnmounted, watch } from 'vue'
import {
  NConfigProvider, NLayout, NLayoutHeader, NLayoutContent, NMessageProvider, NTag, NButton,
  NTabs, NTabPane, zhCN, dateZhCN,
} from 'naive-ui'
import ChatPanel from './components/ChatPanel.vue'
import LoginPanel from './components/LoginPanel.vue'
import DocumentPanel from './components/DocumentPanel.vue'
import KnowledgeBasePanel from './components/KnowledgeBasePanel.vue'
import { useChatStore } from './stores/chat'
import { useAuthStore } from './stores/auth'
import { useDocumentStore } from './stores/documents'

const chat = useChatStore()
const auth = useAuthStore()
const documents = useDocumentStore()

// 「问答」为默认视图；「新会话」按钮仍固定在 header，不随视图切换
const activeTab = ref('qa')

// 主题微调（P0 骨架先给个基础主题）
const themeOverrides = {
  common: {
    primaryColor: '#2080f0',
  },
}

function logout() {
  auth.logout()
  chat.reset()
}

// 全局 401 事件：令牌失效时退回登录页
function onUnauthorized() {
  auth.logout()
}

onMounted(() => {
  window.addEventListener('knowvault:unauthorized', onUnauthorized)
})

// 登录成功（isAuthenticated 由 false → true）时初始化数据；immediate 覆盖"刷新页面时已登录"的场景
watch(
  () => auth.isAuthenticated,
  (isAuthenticated) => {
    if (isAuthenticated) {
      chat.checkConnection()
      documents.init()
    }
  },
  { immediate: true },
)

onUnmounted(() => {
  window.removeEventListener('knowvault:unauthorized', onUnauthorized)
})
</script>

<style>
html, body, #app { margin: 0; height: 100%; }
</style>

<style scoped>
.app-tabs { height: 100%; }
.layout { display: flex; gap: 24px; height: 100%; }
.side { width: 340px; flex: none; overflow-y: auto; }
.main { flex: 1; min-width: 0; }
</style>

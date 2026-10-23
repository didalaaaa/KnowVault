<template>
  <n-config-provider :theme-overrides="themeOverrides" :locale="zhCN" :date-locale="dateZhCN">
    <n-message-provider>
      <LoginPanel v-if="!auth.isAuthenticated" />
      <div v-else class="app-shell">
        <header class="app-header">
          <div class="header-inner">
            <div class="brand">
              <span class="brand-mark"><KvIcon name="book-open" :size="19" :stroke-width="1.9" /></span>
              <span class="brand-name">KnowVault</span>
              <span class="brand-sub">智库</span>
            </div>

            <div class="header-actions">
              <span class="conn-status" :class="{ online: chat.connected }">
                <span class="conn-dot"></span>
                {{ chat.connected ? '服务在线' : '服务离线' }}
              </span>
              <n-button size="small" quaternary class="header-btn" @click="chat.reset()">
                <template #icon><KvIcon name="refresh" :size="15" /></template>
                新会话
              </n-button>
              <n-button size="small" quaternary class="header-btn" @click="logout">
                <template #icon><KvIcon name="logout" :size="15" /></template>
                退出
              </n-button>
            </div>
          </div>
        </header>

        <main class="app-main">
          <div class="workspace">
            <n-tabs v-model:value="activeTab" type="segment" size="medium" animated class="app-tabs">
              <n-tab-pane name="qa" tab="问答" display-directive="show:lazy" class="qa-pane">
                <div class="qa-layout">
                  <aside class="qa-side"><DocumentPanel /></aside>
                  <section class="qa-main"><ChatPanel /></section>
                </div>
              </n-tab-pane>
              <n-tab-pane name="kb" tab="知识库" display-directive="show:lazy" class="kb-pane">
                <KnowledgeBasePanel />
              </n-tab-pane>
            </n-tabs>
          </div>
        </main>
      </div>
    </n-message-provider>
  </n-config-provider>
</template>

<script setup>
import { ref, onMounted, onUnmounted, watch } from 'vue'
import { NConfigProvider, NMessageProvider, NButton, NTabs, NTabPane, zhCN, dateZhCN } from 'naive-ui'
import ChatPanel from './components/ChatPanel.vue'
import LoginPanel from './components/LoginPanel.vue'
import DocumentPanel from './components/DocumentPanel.vue'
import KnowledgeBasePanel from './components/KnowledgeBasePanel.vue'
import KvIcon from './components/KvIcon.vue'
import { useChatStore } from './stores/chat'
import { useAuthStore } from './stores/auth'
import { useDocumentStore } from './stores/documents'

const chat = useChatStore()
const auth = useAuthStore()
const documents = useDocumentStore()

// 「问答」为默认视图；「新会话」按钮仍固定在 header，不随视图切换
const activeTab = ref('qa')

// 墨绿主色 + 暖灰中性色：与全局设计令牌保持一致
const themeOverrides = {
  common: {
    primaryColor: '#0F766E',
    primaryColorHover: '#12857C',
    primaryColorPressed: '#0B5D57',
    primaryColorSuppl: '#12857C',
    infoColor: '#3B6F8F',
    successColor: '#2F7D5B',
    warningColor: '#B45309',
    errorColor: '#B9403A',
    textColorBase: '#17221D',
    textColor1: '#17221D',
    textColor2: '#3F4C45',
    textColor3: '#6B7670',
    borderColor: '#E4E7DF',
    dividerColor: '#EAEDE6',
    bodyColor: '#F3F4EF',
    cardColor: '#FFFFFF',
    modalColor: '#FFFFFF',
    popoverColor: '#FFFFFF',
    tableColor: '#FFFFFF',
    inputColor: '#FFFFFF',
    hoverColor: '#F4F6F1',
    borderRadius: '10px',
    borderRadiusSmall: '8px',
    fontFamily: '"Inter", "Segoe UI", "PingFang SC", "HarmonyOS Sans SC", "Microsoft YaHei", sans-serif',
    fontSize: '15px',
    fontSizeMedium: '15px',
    lineHeight: '1.65',
    boxShadow1: '0 2px 8px rgba(23, 34, 29, 0.06)',
    boxShadow2: '0 8px 24px rgba(23, 34, 29, 0.08)',
    boxShadow3: '0 18px 48px rgba(23, 34, 29, 0.12)',
  },
  Button: {
    borderRadiusMedium: '10px',
    borderRadiusSmall: '8px',
    fontWeight: '600',
    fontWeightStrong: '700',
    heightMedium: '38px',
    heightSmall: '32px',
    paddingMedium: '0 16px',
    paddingSmall: '0 12px',
  },
  Input: {
    borderRadius: '10px',
    heightMedium: '40px',
    heightLarge: '46px',
  },
  Card: {
    borderRadius: '16px',
    borderColor: '#E4E7DF',
  },
  Tag: {
    borderRadius: '999px',
  },
  Tabs: {
    tabBorderRadius: '9px',
    tabFontWeightActive: '600',
    tabTextColorActive: '#0F766E',
    tabTextColorActiveSegment: '#0F766E',
    tabTextColorHoverSegment: '#17221D',
    tabColorSegment: '#E9ECE6',
    tabColorSegmentActive: '#FFFFFF',
    tabBoxShadowActiveSegment: '0 1px 2px rgba(23, 34, 29, 0.08)',
  },
  Alert: {
    borderRadius: '12px',
  },
  Select: {
    peers: {
      InternalSelection: {
        borderRadius: '10px',
        heightMedium: '38px',
      },
    },
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
      documents.refresh()
    }
  },
  { immediate: true },
)

onUnmounted(() => {
  window.removeEventListener('knowvault:unauthorized', onUnauthorized)
})
</script>

<style scoped>
.app-shell {
  height: 100vh;
  overflow: hidden;
  display: flex;
  flex-direction: column;
}

.app-header {
  position: sticky;
  top: 0;
  z-index: 20;
  background: rgba(248, 249, 245, 0.84);
  backdrop-filter: blur(14px);
  border-bottom: 1px solid var(--kv-border);
}

.header-inner {
  max-width: 1280px;
  height: 64px;
  margin: 0 auto;
  padding: 0 28px;
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 20px;
}

.brand {
  display: flex;
  align-items: center;
  gap: 10px;
  min-width: 0;
}

.brand-mark {
  width: 34px;
  height: 34px;
  flex: none;
  display: grid;
  place-items: center;
  border-radius: 10px;
  color: #ffffff;
  background: linear-gradient(140deg, #12857c, #0b4f4a);
  box-shadow: 0 4px 12px rgba(15, 118, 110, 0.24), inset 0 1px 0 rgba(255, 255, 255, 0.22);
}

.brand-name {
  font-size: 17px;
  font-weight: 700;
  letter-spacing: -0.01em;
  color: var(--kv-text);
}

.brand-sub {
  padding-left: 10px;
  border-left: 1px solid var(--kv-border-strong);
  font-size: 12px;
  color: var(--kv-text-3);
}

.header-actions {
  display: flex;
  align-items: center;
  gap: 2px;
}

.conn-status {
  display: inline-flex;
  align-items: center;
  gap: 7px;
  margin-right: 8px;
  padding: 5px 10px;
  border: 1px solid var(--kv-border);
  border-radius: 999px;
  background: var(--kv-surface-soft);
  font-size: 12px;
  color: var(--kv-text-3);
}

.conn-dot {
  width: 6px;
  height: 6px;
  border-radius: 50%;
  background: #a9b0aa;
}

.conn-status.online {
  color: var(--kv-success);
  background: #eef7f2;
  border-color: #d7e9df;
}

.conn-status.online .conn-dot {
  background: var(--kv-success);
  box-shadow: 0 0 0 3px rgba(47, 125, 91, 0.12);
}

.header-btn {
  color: var(--kv-text-2);
}

.app-main {
  flex: 1;
  min-height: 0;
  overflow: hidden;
  padding: 22px 28px 26px;
}

.workspace {
  max-width: 1280px;
  height: 100%;
  margin: 0 auto;
  display: flex;
  flex-direction: column;
}

.app-tabs {
  flex: 1;
  min-height: 0;
  display: flex;
  flex-direction: column;
}

.app-tabs :deep(.n-tabs-nav) {
  flex: none;
  width: fit-content;
  margin-bottom: 16px;
}

.app-tabs :deep(.n-tabs-nav--segment) {
  padding: 4px;
  border-radius: 12px;
  background: #e9ece6;
}

.app-tabs :deep(.n-tabs-tab) {
  height: 34px;
  padding: 0 18px;
  border-radius: 9px;
  font-weight: 500;
}

.app-tabs :deep(.n-tabs-tab.n-tabs-tab--active) {
  background: #ffffff;
  color: var(--kv-primary);
  box-shadow: 0 1px 2px rgba(23, 34, 29, 0.08);
}

.app-tabs :deep(.n-tabs-pane-wrapper) {
  flex: 1;
  min-height: 0;
}

.app-tabs :deep(.n-tab-pane) {
  height: 100%;
}

.qa-layout {
  height: 100%;
  min-height: 0;
  display: grid;
  grid-template-columns: 336px minmax(0, 1fr);
  gap: 20px;
}

.qa-side,
.qa-main {
  min-height: 0;
}

.kb-pane {
  overflow-y: auto;
  padding-bottom: 12px;
}

@media (max-width: 1100px) {
  .qa-layout {
    grid-template-columns: 300px minmax(0, 1fr);
  }
}

@media (max-width: 900px) {
  .app-shell {
    height: auto;
    min-height: 100vh;
    overflow: visible;
  }

  .header-inner {
    padding: 0 16px;
  }

  .app-main {
    overflow: visible;
    min-height: calc(100vh - 64px);
    padding: 16px;
  }

  .workspace {
    height: auto;
  }

  .app-tabs :deep(.n-tabs-pane-wrapper) {
    min-height: auto;
  }

  .qa-layout {
    height: auto;
    grid-template-columns: 1fr;
  }

  .qa-side {
    height: 46vh;
  }

  .qa-main {
    height: 68vh;
    min-height: 460px;
  }
}

@media (max-width: 640px) {
  .brand-sub,
  .conn-status {
    display: none;
  }

  .header-inner {
    gap: 10px;
  }

  .header-actions {
    gap: 0;
  }

  .app-main {
    padding: 12px;
  }

  .qa-side {
    height: 40vh;
  }
}
</style>

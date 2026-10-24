<template>
  <n-config-provider :theme-overrides="themeOverrides" :locale="zhCN" :date-locale="dateZhCN">
    <n-message-provider>
      <LoginPanel v-if="!auth.isAuthenticated" />
      <div v-else class="app-shell" :inert="documents.deletingId ? true : undefined">
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
              <n-button
                size="small"
                quaternary
                class="header-btn"
                aria-label="打开个人中心"
                @click="profileVisible = true"
              >
                <template #icon><KvIcon name="user" :size="15" /></template>
                <span class="profile-btn-name">{{ auth.displayName || '我的账号' }}</span>
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
                  <aside class="qa-side">
                    <n-tabs v-model:value="sideView" type="segment" size="small" animated class="side-tabs">
                      <n-tab-pane name="docs" tab="资料" display-directive="show:lazy" class="side-pane">
                        <DocumentPanel />
                      </n-tab-pane>
                      <n-tab-pane name="chats" tab="会话" display-directive="show:lazy" class="side-pane">
                        <ConversationSidebar />
                      </n-tab-pane>
                    </n-tabs>
                  </aside>
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

      <!-- 删除进行中：全局遮罩阻断所有操作，避免并发改动其它数据 -->
      <div v-if="auth.isAuthenticated && documents.deletingId" class="global-mask" role="status" aria-live="polite">
        <div class="global-mask-card">
          <n-spin :size="22" />
          <span>正在删除文档，请稍候…</span>
        </div>
      </div>

      <ProfileModal v-if="auth.isAuthenticated" v-model:show="profileVisible" @saved="onProfileSaved" />
    </n-message-provider>
  </n-config-provider>
</template>

<script setup>
import { ref, onMounted, onUnmounted, watch } from 'vue'
import { NConfigProvider, NMessageProvider, NButton, NTabs, NTabPane, NSpin, zhCN, dateZhCN } from 'naive-ui'
import ChatPanel from './components/ChatPanel.vue'
import LoginPanel from './components/LoginPanel.vue'
import ConversationSidebar from './components/ConversationSidebar.vue'
import DocumentPanel from './components/DocumentPanel.vue'
import KnowledgeBasePanel from './components/KnowledgeBasePanel.vue'
import ProfileModal from './components/ProfileModal.vue'
import KvIcon from './components/KvIcon.vue'
import { useChatStore } from './stores/chat'
import { useConversationStore } from './stores/conversations'
import { useAuthStore } from './stores/auth'
import { useDocumentStore } from './stores/documents'

const chat = useChatStore()
const conversations = useConversationStore()
const auth = useAuthStore()
const documents = useDocumentStore()

// 「问答」为默认视图；「新会话」按钮仍固定在 header，不随视图切换
const activeTab = ref('qa')
// 左侧栏视图：「资料」上传文档，「会话」回放历史问答
const sideView = ref('docs')

// 个人中心弹窗；资料由 auth store 缓存，header 直接展示显示名
const profileVisible = ref(false)

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
  profileVisible.value = false
  documents.stopPolling()
  conversations.reset()
  auth.logout()
  chat.reset()
}

/**
 * 个人中心保存成功：同步 header 展示的显示名。
 * @param {object} profile 最新资料
 */
function onProfileSaved(profile) {
  auth.setProfile(profile)
}

// 全局 401 事件：令牌失效时退回登录页
function onUnauthorized() {
  profileVisible.value = false
  documents.stopPolling()
  conversations.reset()
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
      auth.loadProfile()
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

.global-mask {
  position: fixed;
  inset: 0;
  /* 高于 Naive UI 弹层（约 2000），低于消息提示（6000） */
  z-index: 5000;
  display: grid;
  place-items: center;
  background: rgba(23, 34, 29, 0.3);
  backdrop-filter: blur(2px);
  cursor: progress;
}

.global-mask-card {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 14px 20px;
  border-radius: 14px;
  background: #ffffff;
  box-shadow: var(--kv-shadow-md);
  font-size: 13.5px;
  color: var(--kv-text-2);
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

.profile-btn-name {
  max-width: 96px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
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
  /* 面板贴满 pane 且带阴影，wrapper 默认的 square overflow 裁切会在底部两角露出灰色直角 */
  border-radius: var(--kv-radius-lg);
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

.qa-side {
  display: flex;
  flex-direction: column;
}

.side-tabs {
  flex: 1;
  min-height: 0;
  display: flex;
  flex-direction: column;
}

.side-tabs :deep(.n-tabs-nav) {
  flex: none;
  width: fit-content;
  padding: 3px;
  border-radius: 11px;
  background: #e9ece6;
}

.side-tabs :deep(.n-tabs-nav--segment) {
  margin-bottom: 10px;
}

.side-tabs :deep(.n-tabs-tab) {
  height: 30px;
  padding: 0 18px;
  border-radius: 8px;
  font-size: 13px;
}

.side-tabs :deep(.n-tabs-pane-wrapper) {
  flex: 1;
  min-height: 0;
  border-radius: var(--kv-radius-lg);
}

.side-tabs :deep(.n-tab-pane) {
  height: 100%;
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
  .conn-status,
  .profile-btn-name {
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

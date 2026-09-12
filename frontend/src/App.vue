<template>
  <n-config-provider :theme-overrides="themeOverrides" :locale="zhCN" :date-locale="dateZhCN">
    <n-message-provider>
      <n-layout style="height: 100vh">
        <n-layout-header bordered style="padding: 16px 24px">
          <div style="display: flex; align-items: center; gap: 12px">
            <span style="font-weight: 700; font-size: 18px">KnowVault · 智库</span>
            <n-tag :type="chat.connected ? 'success' : 'default'" size="small" round>
              后端 {{ chat.connected ? '已连接' : '未连接' }}
            </n-tag>
          </div>
        </n-layout-header>
        <n-layout-content content-style="padding: 24px; height: 100%">
          <ChatPanel />
        </n-layout-content>
      </n-layout>
    </n-message-provider>
  </n-config-provider>
</template>

<script setup>
import { onMounted } from 'vue'
import {
  NConfigProvider, NLayout, NLayoutHeader, NLayoutContent, NMessageProvider, NTag,
  zhCN, dateZhCN,
} from 'naive-ui'
import ChatPanel from './components/ChatPanel.vue'
import { useChatStore } from './stores/chat'

const chat = useChatStore()

// 主题微调（P0 骨架先给个基础主题）
const themeOverrides = {
  common: {
    primaryColor: '#2080f0',
  },
}

onMounted(() => {
  chat.checkConnection()
})
</script>

<style>
html, body, #app { margin: 0; height: 100%; }
</style>

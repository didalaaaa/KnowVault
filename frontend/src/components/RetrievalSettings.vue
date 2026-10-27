<template>
  <n-popover trigger="click" placement="bottom-end" :show-arrow="false" raw>
    <template #trigger>
      <n-button size="small" quaternary class="retrieval-trigger" aria-label="检索设置">
        <template #icon><KvIcon name="settings" :size="14" /></template>
        检索设置
      </n-button>
    </template>

    <div class="retrieval-panel">
      <div class="panel-title">检索设置</div>

      <div class="setting-row">
        <div class="setting-text">
          <strong>混合检索</strong>
          <span>向量召回 + 关键词召回，融合后重排</span>
        </div>
        <n-switch size="small" :value="chat.hybridEnabled" @update:value="onHybrid" />
      </div>

      <div class="setting-row">
        <div class="setting-text">
          <strong>查询改写</strong>
          <span>每次提问会多一次模型调用</span>
        </div>
        <n-switch size="small" :value="chat.rewriteEnabled" @update:value="onRewrite" />
      </div>

      <p class="panel-foot">只影响之后的提问，历史回答不受影响</p>
    </div>
  </n-popover>
</template>

<script setup>
import { NButton, NPopover, NSwitch } from 'naive-ui'
import KvIcon from './KvIcon.vue'
import { useChatStore } from '../stores/chat'

const chat = useChatStore()

/**
 * 切换混合检索开关
 * @param {boolean} value 开关值
 */
function onHybrid(value) {
  chat.setHybrid(value)
}

/**
 * 切换查询改写开关
 * @param {boolean} value 开关值
 */
function onRewrite(value) {
  chat.setRewrite(value)
}
</script>

<style scoped>
.retrieval-trigger {
  flex: none;
}

.retrieval-panel {
  width: 280px;
  padding: 14px 16px 12px;
  border: 1px solid var(--kv-border);
  border-radius: var(--kv-radius-lg);
  background: var(--kv-surface);
  box-shadow: var(--kv-shadow-md);
}

.panel-title {
  margin-bottom: 10px;
  font-size: 12px;
  font-weight: 650;
  letter-spacing: 0.04em;
  color: var(--kv-text-3);
  text-transform: uppercase;
}

.setting-row {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 12px;
  padding: 9px 0;
  border-top: 1px solid var(--kv-border);
}

.setting-text {
  min-width: 0;
  display: flex;
  flex-direction: column;
  gap: 3px;
}

.setting-text strong {
  font-size: 13px;
  font-weight: 600;
  color: var(--kv-text);
}

.setting-text span {
  font-size: 11.5px;
  line-height: 1.5;
  color: var(--kv-text-3);
}

.panel-foot {
  margin: 8px 0 0;
  font-size: 11px;
  color: var(--kv-text-3);
}
</style>

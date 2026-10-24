<template>
  <div class="login-wrap">
    <section class="login-story">
      <div class="story-inner">
        <div class="story-brand">
          <span class="brand-mark"><KvIcon name="book-open" :size="20" :stroke-width="1.9" /></span>
          <span class="brand-name">KnowVault · 智库</span>
        </div>

        <h1>你问，<br /><em>它从你的文档里找答案。</em></h1>
        <p class="story-lead">
          把产品手册、会议纪要与研究资料交给 KnowVault。系统会自动解析、切片并建立语义索引，
          回答只基于你的资料，并逐条标注出处。
        </p>

        <ul class="story-points">
          <li>
            <span class="point-icon"><KvIcon name="search" :size="16" /></span>
            语义检索，不必记住原文关键词
          </li>
          <li>
            <span class="point-icon"><KvIcon name="quote" :size="16" /></span>
            每个回答都能追溯到具体文档片段
          </li>
          <li>
            <span class="point-icon"><KvIcon name="shield" :size="16" /></span>
            私有资料与知识库权限隔离
          </li>
        </ul>

        <div class="story-foot">RAG Knowledge Base · 语义检索 · 引用溯源</div>
      </div>
    </section>

    <section class="login-panel">
      <RegisterPanel v-if="mode === 'register'" @back="switchTo('login')" @registered="onRegistered" />
      <div v-else class="auth-card">
        <div class="auth-head">
          <h2>欢迎回来</h2>
          <p>登录后继续你的知识工作</p>
        </div>

        <n-form :disabled="auth.loading" @submit.prevent="submit">
          <n-form-item label="用户名">
            <n-input
              v-model:value="username"
              placeholder="admin"
              autocomplete="username"
              @keydown.enter.prevent="submit"
            />
          </n-form-item>
          <n-form-item label="密码">
            <n-input
              v-model:value="password"
              type="password"
              show-password-on="click"
              autocomplete="current-password"
              @keydown.enter.prevent="submit"
            />
          </n-form-item>
          <n-alert v-if="auth.error" type="error" :show-icon="false" class="auth-alert">
            {{ auth.error }}
          </n-alert>
          <n-button type="primary" block class="auth-submit" :loading="auth.loading" @click="submit">
            登录
          </n-button>
        </n-form>

        <p class="auth-note">
          还没有账号？
          <button type="button" class="auth-link" @click="switchTo('register')">立即注册</button>
        </p>
      </div>
    </section>
  </div>
</template>

<script setup>
import { ref } from 'vue'
import { NForm, NFormItem, NInput, NButton, NAlert, useMessage } from 'naive-ui'
import KvIcon from './KvIcon.vue'
import RegisterPanel from './RegisterPanel.vue'
import { useAuthStore } from '../stores/auth'

const auth = useAuthStore()
const message = useMessage()
const username = ref('')
const password = ref('')
// login / register 两种视图；注册成功后回到登录并预填用户名
const mode = ref('login')

// 提交登录：成功后 App.vue 会依据 isAuthenticated 切到主界面
async function submit() {
  if (!username.value || !password.value) return
  await auth.login({ username: username.value, password: password.value })
}

/**
 * 切换登录 / 注册视图，并清掉上一次的登录错误。
 * @param {'login'|'register'} next 目标视图
 */
function switchTo(next) {
  auth.error = ''
  mode.value = next
}

/**
 * 注册成功：回到登录视图并预填用户名。
 * @param {string} userName 新账号用户名
 */
function onRegistered(userName) {
  username.value = userName
  password.value = ''
  switchTo('login')
  message.success('注册成功，请登录')
}
</script>

<style scoped>
.login-wrap {
  min-height: 100vh;
  display: grid;
  grid-template-columns: minmax(0, 1.08fr) minmax(420px, 0.92fr);
  background: var(--kv-bg);
}

.login-story {
  position: relative;
  overflow: hidden;
  display: flex;
  align-items: center;
  padding: 64px clamp(32px, 7vw, 96px);
  color: #eef7f3;
  background:
    radial-gradient(circle at 20% 18%, rgba(45, 212, 191, 0.16), transparent 42%),
    linear-gradient(155deg, #0b3b36 0%, #0b2b28 58%, #081f1d 100%);
}

.login-story::before {
  content: "";
  position: absolute;
  inset: 0;
  background-image:
    linear-gradient(rgba(255, 255, 255, 0.045) 1px, transparent 1px),
    linear-gradient(90deg, rgba(255, 255, 255, 0.045) 1px, transparent 1px);
  background-size: 44px 44px;
  mask-image: radial-gradient(circle at 30% 30%, #000 0%, transparent 72%);
  pointer-events: none;
}

.login-story::after {
  content: "";
  position: absolute;
  right: -140px;
  bottom: -170px;
  width: 430px;
  height: 430px;
  border-radius: 50%;
  background: radial-gradient(circle, rgba(180, 83, 9, 0.22), transparent 65%);
  filter: blur(10px);
  pointer-events: none;
}

.story-inner {
  position: relative;
  z-index: 1;
  max-width: 520px;
}

.story-brand {
  display: flex;
  align-items: center;
  gap: 10px;
  margin-bottom: 52px;
}

.brand-mark {
  width: 38px;
  height: 38px;
  display: grid;
  place-items: center;
  border-radius: 11px;
  color: #ffffff;
  background: linear-gradient(140deg, #1aa093, #0b4f4a);
  box-shadow: 0 6px 18px rgba(0, 0, 0, 0.22), inset 0 1px 0 rgba(255, 255, 255, 0.24);
}

.brand-name {
  font-size: 16px;
  font-weight: 700;
  letter-spacing: 0.02em;
}

.story-inner h1 {
  margin: 0;
  font-size: clamp(30px, 3.5vw, 42px);
  line-height: 1.28;
  letter-spacing: -0.03em;
  font-weight: 700;
  text-wrap: balance;
}

.story-inner h1 em {
  font-style: normal;
  color: #7fd8c9;
}

.story-lead {
  max-width: 460px;
  margin: 20px 0 0;
  font-size: 15px;
  line-height: 1.85;
  color: rgba(238, 247, 243, 0.72);
}

.story-points {
  list-style: none;
  display: flex;
  flex-direction: column;
  gap: 14px;
  margin: 34px 0 0;
  padding: 0;
}

.story-points li {
  display: flex;
  align-items: center;
  gap: 12px;
  font-size: 13.5px;
  color: rgba(238, 247, 243, 0.86);
}

.point-icon {
  width: 32px;
  height: 32px;
  flex: none;
  display: grid;
  place-items: center;
  border: 1px solid rgba(255, 255, 255, 0.1);
  border-radius: 9px;
  color: #9fe3d6;
  background: rgba(255, 255, 255, 0.07);
}

.story-foot {
  margin-top: 54px;
  font-size: 11px;
  letter-spacing: 0.16em;
  text-transform: uppercase;
  color: rgba(238, 247, 243, 0.4);
}

.login-panel {
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 40px 28px;
  background: var(--kv-bg);
}

@media (max-width: 900px) {
  .login-wrap {
    grid-template-columns: 1fr;
  }

  .login-story {
    display: none;
  }

  .login-panel {
    min-height: 100vh;
  }
}

@media (max-width: 480px) {
  .login-panel {
    padding: 20px 16px;
  }
}
</style>

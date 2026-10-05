<template>
  <div class="login-wrap">
    <n-card title="KnowVault · 智库" class="login-card">
      <n-form :disabled="auth.loading" @submit.prevent="submit">
        <n-form-item label="用户名">
          <n-input v-model:value="username" placeholder="admin" @keydown.enter.prevent="submit" />
        </n-form-item>
        <n-form-item label="密码">
          <n-input
            v-model:value="password"
            type="password"
            show-password-on="click"
            @keydown.enter.prevent="submit"
          />
        </n-form-item>
        <n-alert v-if="auth.error" type="error" :show-icon="false" style="margin-bottom: 12px">
          {{ auth.error }}
        </n-alert>
        <n-button type="primary" block :loading="auth.loading" @click="submit">登录</n-button>
      </n-form>
    </n-card>
  </div>
</template>

<script setup>
import { ref } from 'vue'
import { NCard, NForm, NFormItem, NInput, NButton, NAlert } from 'naive-ui'
import { useAuthStore } from '../stores/auth'

const auth = useAuthStore()
const username = ref('')
const password = ref('')

// 提交登录：成功后 App.vue 会依据 isAuthenticated 切到主界面
async function submit() {
  if (!username.value || !password.value) return
  await auth.login({ username: username.value, password: password.value })
}
</script>

<style scoped>
.login-wrap {
  display: flex;
  align-items: center;
  justify-content: center;
  height: 100vh;
  background: #f5f7fa;
}
.login-card {
  width: 380px;
}
</style>

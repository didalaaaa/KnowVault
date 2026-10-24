<template>
  <div class="auth-card">
    <div class="auth-head">
      <h2>创建账号</h2>
      <p>注册后可新建知识库、上传文档并提问</p>
    </div>

    <n-form
      ref="formRef"
      :model="form"
      :rules="rules"
      :disabled="loading"
      @submit.prevent="submit"
    >
      <n-form-item label="用户名" path="userName">
        <n-input
          v-model:value="form.userName"
          placeholder="3–32 位字母、数字或下划线"
          autocomplete="username"
          maxlength="32"
          autofocus
        />
      </n-form-item>
      <n-form-item label="邮箱" path="email">
        <n-input
          v-model:value="form.email"
          placeholder="用于找回账号"
          autocomplete="email"
          maxlength="256"
        />
      </n-form-item>
      <n-form-item label="显示名（可选）" path="displayName">
        <n-input v-model:value="form.displayName" placeholder="留空则使用用户名" maxlength="64" />
      </n-form-item>
      <n-form-item label="密码" path="password">
        <n-input
          v-model:value="form.password"
          type="password"
          show-password-on="click"
          placeholder="至少 6 位，含大小写字母、数字和特殊字符"
          autocomplete="new-password"
          maxlength="64"
        />
      </n-form-item>

      <n-alert v-if="errorText" type="error" :show-icon="false" class="auth-alert">
        {{ errorText }}
      </n-alert>

      <n-button type="primary" block class="auth-submit" :loading="loading" @click="submit">
        注册
      </n-button>
    </n-form>

    <p class="auth-note">
      已有账号？
      <button type="button" class="auth-link" @click="emit('back')">返回登录</button>
    </p>
  </div>
</template>

<script setup>
import { reactive, ref } from 'vue'
import { NAlert, NButton, NForm, NFormItem, NInput } from 'naive-ui'
import { accountApi } from '../api/account'

const emit = defineEmits(['back', 'registered'])

const formRef = ref(null)
const loading = ref(false)
const errorText = ref('')
const form = reactive({ userName: '', email: '', displayName: '', password: '' })

const rules = {
  userName: [
    { required: true, message: '请输入用户名', trigger: ['input', 'blur'] },
    { min: 3, max: 32, message: '用户名长度需为 3–32 位', trigger: ['input', 'blur'] },
  ],
  email: [
    { required: true, message: '请输入邮箱', trigger: ['input', 'blur'] },
    { type: 'email', message: '邮箱格式不正确', trigger: ['input', 'blur'] },
  ],
  password: [
    { required: true, message: '请输入密码', trigger: ['input', 'blur'] },
    { min: 6, max: 64, message: '密码长度需为 6–64 位', trigger: ['input', 'blur'] },
  ],
}

/** 提交注册；成功后回到登录页并预填用户名。 */
async function submit() {
  if (loading.value) return
  errorText.value = ''

  try {
    await formRef.value?.validate()
  } catch {
    return
  }

  loading.value = true
  try {
    await accountApi.register({
      userName: form.userName.trim(),
      email: form.email.trim(),
      displayName: form.displayName.trim() || null,
      password: form.password,
    })
    emit('registered', form.userName.trim())
  } catch (e) {
    errorText.value = describeError(e)
  } finally {
    loading.value = false
  }
}

/**
 * 把未知错误整理成一行可读文案：优先取字段校验文案，其次取 ABP 友好异常文案。
 * @param {any} e 错误对象
 * @returns {string} 错误文案
 */
function describeError(e) {
  const error = e?.response?.data?.error
  const details = (error?.validationErrors || []).map((item) => item.message).filter(Boolean)
  if (details.length > 0) return details.join('；')
  return error?.message || e?.message || '注册失败'
}
</script>

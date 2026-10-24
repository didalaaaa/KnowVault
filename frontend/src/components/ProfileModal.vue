<template>
  <n-modal
    :show="show"
    preset="card"
    title="个人中心"
    style="width: min(460px, calc(100vw - 32px))"
    @update:show="(value) => emit('update:show', value)"
  >
    <div v-if="loading" class="profile-loading" aria-live="polite">
      <n-spin :size="20" />
      <span>正在加载资料…</span>
    </div>

    <template v-else>
      <div class="profile-head">
        <span class="profile-avatar" aria-hidden="true">{{ initial(profile?.userName) }}</span>
        <div class="profile-identity">
          <strong>{{ profile?.userName || '—' }}</strong>
          <span>{{ profile?.email || '未设置邮箱' }}</span>
        </div>
        <div class="profile-roles" aria-label="账号角色">
          <n-tag v-for="role in profile?.roles || []" :key="role" size="tiny" :bordered="false">
            {{ role }}
          </n-tag>
        </div>
      </div>

      <section class="profile-section">
        <h3 class="profile-section-title">基本资料</h3>
        <p class="profile-section-hint">显示名会出现在右上角，邮箱用于找回账号</p>
        <n-form :model="profileForm" label-placement="top" @submit.prevent="saveProfile">
          <n-form-item label="显示名">
            <n-input v-model:value="profileForm.displayName" maxlength="64" placeholder="留空则使用用户名" />
          </n-form-item>
          <n-form-item label="邮箱">
            <n-input v-model:value="profileForm.email" maxlength="256" autocomplete="email" />
          </n-form-item>
          <div class="profile-actions">
            <n-button
              type="primary"
              :loading="savingProfile"
              :disabled="!profileForm.email.trim()"
              @click="saveProfile"
            >
              保存资料
            </n-button>
          </div>
        </n-form>
      </section>

      <section class="profile-section">
        <h3 class="profile-section-title">修改密码</h3>
        <p class="profile-section-hint">修改后当前登录保持有效，下次登录请使用新密码</p>
        <n-form :model="passwordForm" label-placement="top" @submit.prevent="changePassword">
          <n-form-item label="当前密码">
            <n-input
              v-model:value="passwordForm.currentPassword"
              type="password"
              show-password-on="click"
              autocomplete="current-password"
            />
          </n-form-item>
          <n-form-item label="新密码">
            <n-input
              v-model:value="passwordForm.newPassword"
              type="password"
              show-password-on="click"
              autocomplete="new-password"
              placeholder="至少 6 位，含大小写字母、数字和特殊字符"
            />
          </n-form-item>
          <n-form-item label="确认新密码">
            <n-input
              v-model:value="passwordForm.confirmPassword"
              type="password"
              show-password-on="click"
              autocomplete="new-password"
            />
          </n-form-item>
          <div class="profile-actions">
            <n-button
              :loading="savingPassword"
              :disabled="!passwordForm.currentPassword || !passwordForm.newPassword"
              @click="changePassword"
            >
              修改密码
            </n-button>
          </div>
        </n-form>
      </section>
    </template>
  </n-modal>
</template>

<script setup>
import { reactive, ref, watch } from 'vue'
import { NButton, NForm, NFormItem, NInput, NModal, NSpin, NTag, useMessage } from 'naive-ui'
import { accountApi } from '../api/account'

const props = defineProps({
  show: { type: Boolean, default: false },
})
const emit = defineEmits(['update:show', 'saved'])

const message = useMessage()
const loading = ref(false)
const savingProfile = ref(false)
const savingPassword = ref(false)
const profile = ref(null)
const profileForm = reactive({ displayName: '', email: '' })
const passwordForm = reactive({ currentPassword: '', newPassword: '', confirmPassword: '' })

watch(
  () => props.show,
  async (show) => {
    if (show) await loadProfile()
  },
)

/** 拉取当前用户资料并填充表单。 */
async function loadProfile() {
  loading.value = true
  try {
    const data = await accountApi.getProfile()
    profile.value = data
    profileForm.displayName = data.displayName || ''
    profileForm.email = data.email || ''
  } catch (e) {
    message.error(describeError(e))
  } finally {
    loading.value = false
  }
}

/** 保存显示名与邮箱。 */
async function saveProfile() {
  savingProfile.value = true
  try {
    const data = await accountApi.updateProfile({
      displayName: profileForm.displayName.trim() || null,
      email: profileForm.email.trim(),
    })
    profile.value = data
    emit('saved', data)
    message.success('资料已保存')
  } catch (e) {
    message.error(describeError(e))
  } finally {
    savingProfile.value = false
  }
}

/** 修改密码；成功后不强制下线，提示下次登录使用新密码。 */
async function changePassword() {
  if (passwordForm.newPassword !== passwordForm.confirmPassword) {
    message.error('两次输入的新密码不一致')
    return
  }

  savingPassword.value = true
  try {
    await accountApi.changePassword({
      currentPassword: passwordForm.currentPassword,
      newPassword: passwordForm.newPassword,
    })
    passwordForm.currentPassword = ''
    passwordForm.newPassword = ''
    passwordForm.confirmPassword = ''
    message.success('密码已修改，下次登录请使用新密码')
  } catch (e) {
    message.error(describeError(e))
  } finally {
    savingPassword.value = false
  }
}

/**
 * 取用户名首字符作为头像占位。
 * @param {string} name 用户名
 * @returns {string} 首字符
 */
function initial(name) {
  return (name || '?').trim().charAt(0).toUpperCase()
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
  return error?.message || e?.message || '操作失败'
}
</script>

<style scoped>
.profile-loading {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 10px;
  padding: 32px 0;
  font-size: 13px;
  color: var(--kv-text-3);
}

.profile-head {
  display: flex;
  align-items: center;
  gap: 12px;
  padding-bottom: 18px;
  border-bottom: 1px solid var(--kv-border);
}

.profile-avatar {
  width: 44px;
  height: 44px;
  flex: none;
  display: grid;
  place-items: center;
  border-radius: 12px;
  font-size: 17px;
  font-weight: 700;
  color: #ffffff;
  background: linear-gradient(140deg, var(--kv-primary), var(--kv-primary-strong));
  box-shadow: 0 4px 12px rgba(15, 118, 110, 0.24);
}

.profile-identity {
  min-width: 0;
  display: flex;
  flex-direction: column;
  gap: 2px;
}

.profile-identity strong {
  font-size: 15px;
  color: var(--kv-text);
}

.profile-identity span {
  font-size: 12px;
  color: var(--kv-text-3);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.profile-roles {
  margin-left: auto;
  display: flex;
  flex-wrap: wrap;
  gap: 4px;
  justify-content: flex-end;
}

.profile-section {
  padding-top: 18px;
}

.profile-section + .profile-section {
  margin-top: 18px;
  border-top: 1px solid var(--kv-border);
}

.profile-section-title {
  margin: 0;
  font-size: 14px;
  font-weight: 600;
  color: var(--kv-text);
}

.profile-section-hint {
  margin: 4px 0 12px;
  font-size: 12px;
  color: var(--kv-text-3);
}

.profile-actions {
  display: flex;
  justify-content: flex-end;
  margin-top: 4px;
}
</style>

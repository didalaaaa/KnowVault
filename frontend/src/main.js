import { createApp } from 'vue'
import { createPinia } from 'pinia'
import App from './App.vue'

// KnowVault 前端入口（Vue 3 + JavaScript，不使用 TypeScript）
const app = createApp(App)
app.use(createPinia())
app.mount('#app')

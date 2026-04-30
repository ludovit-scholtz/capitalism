import { createPinia } from 'pinia'
import { createApp as createVueApp } from 'vue'

import App from './App.vue'
import { i18n } from './i18n'
import router from './router'
import './assets/styles/main.css'

export function createApp() {
  const app = createVueApp(App)
  const pinia = createPinia()

  app.use(pinia)
  app.use(i18n)
  app.use(router)

  return { app, router }
}

const { app } = createApp()
app.mount('#app')

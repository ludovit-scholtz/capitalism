import { createApp as createVueApp } from 'vue'

import App from './App.vue'
import router from './router'
import './assets/styles/main.css'

export function createApp() {
  const app = createVueApp(App)

  app.use(router)

  return { app, router }
}

const { app } = createApp()
app.mount('#app')
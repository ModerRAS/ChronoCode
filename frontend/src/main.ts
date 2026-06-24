import { createApp } from 'vue'
import Antd from 'ant-design-vue'
import router from './router'
import App from './App.vue'
import 'ant-design-vue/dist/reset.css'
import '@vue-flow/core/dist/style.css'
import './style.css'

const app = createApp(App)
app.use(Antd)
app.use(router)
app.mount('#app')

import { createApp, watch } from 'vue'
import { createVuetify } from 'vuetify'
import { mdi } from 'vuetify/iconsets/mdi-svg'
import { de, en, es, fr, it, ko, pt, vi, zhHans } from 'vuetify/locale'
import 'vuetify/styles'
import App from './App.vue'
import './styles.css'
import { vuetifyIconAliases } from './icons'
import {
  resolveThemeName,
  themePreference,
} from './themePreference'
import { reportLanguage } from './localization'

const colorScheme = window.matchMedia('(prefers-color-scheme: dark)')
const vuetifyMessages = {
  'en-US': en,
  'it-IT': it,
  'fr-FR': fr,
  'de-DE': de,
  'es-ES': es,
  'zh-Hans': zhHans,
  'vi-VN': vi,
  'ko-KR': ko,
  'pt-PT': pt,
  'pt-BR': pt,
}

const vuetify = createVuetify({
  icons: {
    defaultSet: 'mdi',
    aliases: vuetifyIconAliases,
    sets: { mdi },
  },
  locale: {
    locale: reportLanguage.value,
    fallback: 'en-US',
    messages: vuetifyMessages,
  },
  theme: {
    defaultTheme: resolveThemeName(themePreference.value, colorScheme.matches),
    themes: {
      trackLight: {
        dark: false,
        colors: {
          background: '#f5f7fa',
          surface: '#ffffff',
          'surface-variant': '#e7edf4',
          'on-surface': '#17202a',
          'on-surface-variant': '#546270',
          primary: '#286da8',
          'primary-darken-1': '#1c537f',
          'primary-lighten-1': '#82b8e3',
          info: '#286da8',
          error: '#b42318',
        },
      },
      trackDark: {
        dark: true,
        colors: {
          background: '#14181d',
          surface: '#1b2027',
          'surface-variant': '#29313a',
          'on-surface': '#f2f6f9',
          'on-surface-variant': '#aebbc6',
          primary: '#78b8ed',
          'primary-darken-1': '#4a93cf',
          'primary-lighten-1': '#b4d7f3',
          info: '#78b8ed',
          error: '#ffb4ab',
        },
      },
    },
  },
})

colorScheme.addEventListener('change', (event) => {
  if (themePreference.value === 'system') {
    vuetify.theme.global.name.value = resolveThemeName('system', event.matches)
  }
})

watch(themePreference, (preference) => {
  vuetify.theme.global.name.value = resolveThemeName(preference, colorScheme.matches)
})

watch(reportLanguage, (language) => {
  vuetify.locale.current.value = language
})

createApp(App)
  .use(vuetify)
  .mount('#app')

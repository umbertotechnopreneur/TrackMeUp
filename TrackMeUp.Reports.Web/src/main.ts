import { createApp, watch } from 'vue'
import { createVuetify } from 'vuetify'
import { aliases, mdi } from 'vuetify/iconsets/mdi'
import { en, it } from 'vuetify/locale'
import { use } from 'echarts/core'
import { BarChart, HeatmapChart } from 'echarts/charts'
import {
  AriaComponent,
  CalendarComponent,
  DataZoomComponent,
  GridComponent,
  LegendComponent,
  TooltipComponent,
  VisualMapComponent,
} from 'echarts/components'
import { CanvasRenderer } from 'echarts/renderers'
import 'vuetify/styles'
import '@mdi/font/css/materialdesignicons.css'
import App from './App.vue'
import './styles.css'
import {
  resolveThemeName,
  themePreference,
} from './themePreference'
import { reportLanguage } from './localization'

use([
  AriaComponent,
  BarChart,
  CalendarComponent,
  CanvasRenderer,
  DataZoomComponent,
  GridComponent,
  HeatmapChart,
  LegendComponent,
  TooltipComponent,
  VisualMapComponent,
])

const colorScheme = window.matchMedia('(prefers-color-scheme: dark)')

const vuetify = createVuetify({
  icons: {
    defaultSet: 'mdi',
    aliases,
    sets: { mdi },
  },
  locale: {
    locale: reportLanguage.value,
    fallback: 'en',
    messages: { en, it },
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

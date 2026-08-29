import { BarChart } from 'echarts/charts'
import {
  AriaComponent,
  GridComponent,
  TooltipComponent,
} from 'echarts/components'
import { use } from 'echarts/core'
import { CanvasRenderer } from 'echarts/renderers'

// Keep registration beside the lazy application-bars view. A first-load of this
// report must not pull heatmap, calendar, visual-map, legend, or data-zoom code.
use([
  AriaComponent,
  BarChart,
  CanvasRenderer,
  GridComponent,
  TooltipComponent,
])

import { HeatmapChart } from 'echarts/charts'
import {
  AriaComponent,
  CalendarComponent,
  TooltipComponent,
  VisualMapContinuousComponent,
} from 'echarts/components'
import { use } from 'echarts/core'
import { CanvasRenderer } from 'echarts/renderers'

// Calendar coordinates are unique to this lazy view. Register only its renderer,
// accessibility layer, tooltip, heatmap, and visual scale before Vue mounts it.
use([
  AriaComponent,
  CalendarComponent,
  CanvasRenderer,
  HeatmapChart,
  TooltipComponent,
  VisualMapContinuousComponent,
])

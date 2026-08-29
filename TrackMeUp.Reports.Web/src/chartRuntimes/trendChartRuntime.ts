import { BarChart } from 'echarts/charts'
import {
  AriaComponent,
  DataZoomComponent,
  GridComponent,
  LegendPlainComponent,
  TooltipComponent,
} from 'echarts/components'
import { use } from 'echarts/core'
import { CanvasRenderer } from 'echarts/renderers'

// Trend is the only view that needs legend and data zoom. Keeping them here makes
// those heavier interactions load only with the already-lazy trend component.
use([
  AriaComponent,
  BarChart,
  CanvasRenderer,
  DataZoomComponent,
  GridComponent,
  LegendPlainComponent,
  TooltipComponent,
])

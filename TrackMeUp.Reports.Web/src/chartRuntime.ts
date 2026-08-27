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
import { use } from 'echarts/core'
import { CanvasRenderer } from 'echarts/renderers'

// Registration is deliberately colocated with the lazy chart components. Importing the
// report shell must not parse ECharts before the user opens a chart-backed report view.
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

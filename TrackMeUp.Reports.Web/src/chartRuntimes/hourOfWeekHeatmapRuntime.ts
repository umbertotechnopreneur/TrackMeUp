import { HeatmapChart } from 'echarts/charts'
import {
  AriaComponent,
  GridComponent,
  TooltipComponent,
  VisualMapContinuousComponent,
} from 'echarts/components'
import { use } from 'echarts/core'
import { CanvasRenderer } from 'echarts/renderers'

// The hour-of-week heatmap is Cartesian, so it intentionally excludes calendar,
// bar, legend, and data-zoom registrations from this lazy-view dependency graph.
use([
  AriaComponent,
  CanvasRenderer,
  GridComponent,
  HeatmapChart,
  TooltipComponent,
  VisualMapContinuousComponent,
])

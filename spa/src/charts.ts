import {
  ArcElement,
  BarElement,
  CategoryScale,
  Chart as ChartJS,
  Filler,
  Legend,
  LineElement,
  LinearScale,
  PointElement,
  Tooltip
} from "chart.js";
import type { DashboardStackedSeries } from "./api";
import { maskA } from "./theme";

ChartJS.register(ArcElement, BarElement, CategoryScale, Filler, Legend, LineElement, LinearScale, PointElement, Tooltip);

ChartJS.defaults.font.family = "Inter, ui-sans-serif, system-ui, sans-serif";
ChartJS.defaults.font.size = 12;
ChartJS.defaults.color = "#5A6B76";
ChartJS.defaults.plugins.legend.labels.boxWidth = 10;
ChartJS.defaults.plugins.legend.labels.boxHeight = 10;
ChartJS.defaults.plugins.legend.labels.padding = 12;
ChartJS.defaults.maintainAspectRatio = false;
ChartJS.defaults.responsive = true;

const FALLBACK: Record<string, string> = {
  queued: maskA.queued,
  processing: maskA.processing,
  ready: maskA.ready,
  failed: maskA.failed,
  needsreview: maskA.review,
  total: maskA.accent
};

const PALETTE = [maskA.accent, maskA.ready, maskA.processing, maskA.review, maskA.failed, maskA.queued];

/** Mask A tokens win for pipeline statuses so charts stay on-theme even if seed colors are older. */
export function seriesColor(key: string, color: string | null | undefined, index = 0): string {
  const mapped = FALLBACK[key.toLowerCase()];
  if (mapped) {
    return mapped;
  }
  if (color && color.trim()) {
    return color;
  }
  return PALETTE[index % PALETTE.length];
}

export function hasSeriesData(series: DashboardStackedSeries[] | undefined): boolean {
  return Boolean(series?.some((item) => item.data.some((value) => value > 0)));
}

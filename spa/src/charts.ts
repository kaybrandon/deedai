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
import { maskF } from "./theme";

ChartJS.register(ArcElement, BarElement, CategoryScale, Filler, Legend, LineElement, LinearScale, PointElement, Tooltip);

ChartJS.defaults.font.family = "Inter, ui-sans-serif, system-ui, sans-serif";
ChartJS.defaults.font.size = 12;
ChartJS.defaults.color = maskF.muted;
ChartJS.defaults.plugins.legend.labels.boxWidth = 10;
ChartJS.defaults.plugins.legend.labels.boxHeight = 10;
ChartJS.defaults.plugins.legend.labels.padding = 12;
ChartJS.defaults.maintainAspectRatio = false;
ChartJS.defaults.responsive = true;

const FALLBACK: Record<string, string> = {
  queued: maskF.queued,
  processing: maskF.processing,
  ready: maskF.ready,
  failed: maskF.failed,
  needsreview: maskF.review,
  total: maskF.teal
};

const PALETTE = [maskF.teal, maskF.accent, maskF.ready, maskF.processing, maskF.review, maskF.failed, maskF.queued];

/** Mask F tokens win for pipeline statuses so charts stay on-theme even if seed colors are older. */
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

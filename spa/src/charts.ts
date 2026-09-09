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

ChartJS.register(ArcElement, BarElement, CategoryScale, Filler, Legend, LineElement, LinearScale, PointElement, Tooltip);

ChartJS.defaults.font.family = "Inter, ui-sans-serif, system-ui, sans-serif";
ChartJS.defaults.font.size = 12;
ChartJS.defaults.color = "#64748b";
ChartJS.defaults.plugins.legend.labels.boxWidth = 10;
ChartJS.defaults.plugins.legend.labels.boxHeight = 10;
ChartJS.defaults.plugins.legend.labels.padding = 12;
ChartJS.defaults.maintainAspectRatio = false;
ChartJS.defaults.responsive = true;

const FALLBACK: Record<string, string> = {
  queued: "#64748b",
  processing: "#4f46e5",
  ready: "#15803d",
  failed: "#dc2626",
  total: "#2563eb"
};

const PALETTE = ["#2563eb", "#0f766e", "#7c3aed", "#d97706", "#db2777", "#0284c7"];

export function seriesColor(key: string, color: string | null | undefined, index = 0): string {
  if (color && color.trim()) {
    return color;
  }
  return FALLBACK[key.toLowerCase()] ?? PALETTE[index % PALETTE.length];
}

export function hasSeriesData(series: DashboardStackedSeries[] | undefined): boolean {
  return Boolean(series?.some((item) => item.data.some((value) => value > 0)));
}

import { Bar, Doughnut, Line } from "react-chartjs-2";
import type { DashboardByUser, DashboardStatusMix, DashboardVolume } from "../api";
import { hasSeriesData, seriesColor } from "../charts";
import EmptyState from "./EmptyState";

const emptyCopy = {
  mix: { title: "No status mix", body: "No deeds in this range. Widen the dates or pick another Client." },
  users: { title: "No by-user activity", body: "No assigned or unassigned deeds in this range." },
  volume: { title: "No volume yet", body: "Upload a PDF or widen the dates to see volume over time." }
};

export function StatusMixChart({ data }: { data: DashboardStatusMix | null }) {
  const slices = data?.series.filter((slice) => slice.count > 0) ?? [];
  if (!data || data.total === 0 || slices.length === 0) {
    return <EmptyState title={emptyCopy.mix.title} body={emptyCopy.mix.body} />;
  }

  return (
    <div className="chart-canvas" role="img" aria-label={`Status mix for ${data.total} deeds`}>
      <Doughnut
        data={{
          labels: slices.map((slice) => slice.label),
          datasets: [
            {
              data: slices.map((slice) => slice.count),
              backgroundColor: slices.map((slice, index) => seriesColor(slice.status, slice.color, index)),
              borderWidth: 0,
              hoverOffset: 4
            }
          ]
        }}
        options={{
          cutout: "62%",
          plugins: {
            legend: { position: "bottom" },
            tooltip: {
              callbacks: {
                label: (item) => {
                  const count = Number(item.raw ?? 0);
                  const pct = data.total ? Math.round((count / data.total) * 100) : 0;
                  return ` ${count} (${pct}%)`;
                }
              }
            }
          }
        }}
      />
    </div>
  );
}

export function ByUserChart({ data }: { data: DashboardByUser | null }) {
  if (!data || data.labels.length === 0 || !hasSeriesData(data.series)) {
    return <EmptyState title={emptyCopy.users.title} body={emptyCopy.users.body} />;
  }

  return (
    <div className="chart-canvas" role="img" aria-label="Deeds by user and status">
      <Bar
        data={{
          labels: data.labels,
          datasets: data.series.map((series, index) => ({
            label: series.label,
            data: series.data,
            backgroundColor: seriesColor(series.key, series.color, index),
            borderSkipped: false,
            borderRadius: 3,
            maxBarThickness: 36
          }))
        }}
        options={{
          plugins: { legend: { position: "bottom" } },
          scales: {
            x: { stacked: true, grid: { display: false } },
            y: { stacked: true, beginAtZero: true, ticks: { precision: 0 }, border: { display: false } }
          }
        }}
      />
    </div>
  );
}

export function VolumeChart({ data }: { data: DashboardVolume | null }) {
  const total = data?.series.find((item) => item.key === "total");
  const statuses = data?.series.filter((item) => item.key !== "total") ?? [];
  if (!data || data.labels.length === 0 || !hasSeriesData(data.series)) {
    return <EmptyState title={emptyCopy.volume.title} body={emptyCopy.volume.body} />;
  }

  return (
    <div className="chart-canvas chart-canvas-wide" role="img" aria-label="Deed volume over time">
      <Line
        data={{
          labels: data.labels,
          datasets: [
            ...(total
              ? [
                  {
                    label: total.label,
                    data: total.data,
                    borderColor: seriesColor(total.key, total.color),
                    backgroundColor: "rgba(79, 124, 138, 0.14)",
                    fill: true,
                    tension: 0.3,
                    pointRadius: 3,
                    pointHoverRadius: 5,
                    borderWidth: 2
                  }
                ]
              : []),
            ...statuses.map((series, index) => ({
              label: series.label,
              data: series.data,
              borderColor: seriesColor(series.key, series.color, index),
              backgroundColor: "transparent",
              fill: false,
              tension: 0.3,
              pointRadius: 2,
              borderWidth: 1.5
            }))
          ]
        }}
        options={{
          interaction: { mode: "index", intersect: false },
          plugins: { legend: { position: "bottom" } },
          scales: {
            x: { grid: { display: false } },
            y: { beginAtZero: true, ticks: { precision: 0 }, border: { display: false } }
          }
        }}
      />
    </div>
  );
}

import type { ActiveElement, Chart, ChartEvent } from "chart.js";
import { Bar, Doughnut } from "react-chartjs-2";
import { Link, useNavigate } from "react-router-dom";
import type { DashboardByUser, DashboardStatusMix, DashboardVolume, DashboardVolumeBucket, DashboardUserColumn } from "../api";
import { hasSeriesData, seriesColor } from "../charts";
import { documentsPath } from "../documentsPath";
import EmptyState from "./EmptyState";

const emptyCopy = {
  mix: { title: "No Status Mix", body: "No deeds in this range. Widen the dates or pick another Client." },
  users: { title: "No By Users Activity", body: "No assigned or unassigned deeds in this range." },
  volume: { title: "No Volume Yet", body: "Upload a PDF or widen the dates to see volume over time." }
};

type ChartNavFilters = {
  clientId?: string;
  from?: string;
  to?: string;
};

function pointerOnHit(event: ChartEvent, elements: ActiveElement[]) {
  const canvas = event.native?.target;
  if (canvas instanceof HTMLCanvasElement) {
    canvas.style.cursor = elements.length ? "pointer" : "default";
  }
}

export function StatusMixChart({
  data,
  clientId = "",
  from = "",
  to = ""
}: {
  data: DashboardStatusMix | null;
} & ChartNavFilters) {
  const navigate = useNavigate();
  const slices = data?.series.filter((slice) => slice.count > 0) ?? [];
  if (!data || data.total === 0 || slices.length === 0) {
    return <EmptyState title={emptyCopy.mix.title} body={emptyCopy.mix.body} />;
  }

  function goStatus(status: string) {
    navigate(documentsPath({ status, clientId, from, to }));
  }

  return (
    <>
      <div className="chart-canvas chart-canvas-clickable" role="img" aria-label={`Status Mix for ${data.total} deeds`}>
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
            onHover: pointerOnHit,
            onClick: (_event, elements) => {
              const slice = slices[elements[0]?.index ?? -1];
              if (slice) goStatus(slice.status);
            },
            plugins: {
              legend: { display: false },
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
      <ul className="chart-legend">
        {slices.map((slice, index) => (
          <li key={slice.status}>
            <Link
              className="chart-legend-link"
              to={documentsPath({ status: slice.status, clientId, from, to })}
              aria-label={`View ${slice.label} documents`}
            >
              <span
                className="chart-legend-swatch"
                style={{ background: seriesColor(slice.status, slice.color, index) }}
              />
              {slice.label}
            </Link>
          </li>
        ))}
      </ul>
    </>
  );
}

export function ByUserChart({
  data,
  clientId = "",
  from = "",
  to = ""
}: {
  data: DashboardByUser | null;
} & ChartNavFilters) {
  const navigate = useNavigate();
  if (!data || data.labels.length === 0 || !hasSeriesData(data.series)) {
    return <EmptyState title={emptyCopy.users.title} body={emptyCopy.users.body} />;
  }

  function goUser(user: DashboardUserColumn | undefined) {
    if (!user?.userId) {
      return;
    }
    navigate(documentsPath({ assigneeUserId: user.userId, clientId, from, to }));
  }

  return (
    <>
      <div className="chart-canvas chart-canvas-clickable" role="img" aria-label="Deeds by Users and status">
        <Bar
          data={{
            labels: data.labels,
            datasets: data.series.map((series, index) => ({
              label: series.label,
              data: series.data,
              backgroundColor: seriesColor(series.key, series.color, index),
              borderSkipped: false,
              borderRadius: 3,
              maxBarThickness: 44
            }))
          }}
          options={{
            interaction: { mode: "index", intersect: false },
            onHover: pointerOnHit,
            onClick: (_event, elements) => goUser(data.users[elements[0]?.index ?? -1]),
            plugins: { legend: { display: false } },
            scales: {
              x: { stacked: true, grid: { display: false } },
              y: { stacked: true, beginAtZero: true, ticks: { precision: 0 }, border: { display: false } }
            }
          }}
        />
      </div>
      <ul className="chart-key" aria-label="Status colors">
        {data.series.map((series, index) => (
          <li key={series.key}>
            <span className="chart-legend-swatch" style={{ background: seriesColor(series.key, series.color, index) }} />
            {series.label}
          </li>
        ))}
      </ul>
      <ul className="chart-legend">
        {data.users.map((user) =>
          user.userId ? (
            <li key={user.userId}>
              <Link
                className="chart-legend-link"
                to={documentsPath({ assigneeUserId: user.userId, clientId, from, to })}
                aria-label={`View documents assigned to ${user.displayName}`}
              >
                {user.displayName}
              </Link>
            </li>
          ) : (
            <li key="unassigned">
              <span className="chart-legend-static">{user.displayName}</span>
            </li>
          )
        )}
      </ul>
    </>
  );
}

function volumeBuckets(data: DashboardVolume): DashboardVolumeBucket[] {
  if (data.buckets?.length === data.labels.length) {
    return data.buckets;
  }
  return data.labels.map((label) => ({ label, from: label, to: label }));
}

function weekIndex(event: ChartEvent, elements: ActiveElement[], chart: Chart) {
  if (elements[0]) {
    return elements[0].index;
  }
  const native = event.native;
  if (!native || !("offsetX" in native) || typeof native.offsetX !== "number") {
    return -1;
  }
  const value = chart.scales.x.getValueForPixel(native.offsetX);
  return typeof value === "number" ? value : -1;
}

export function VolumeChart({
  data,
  clientId = ""
}: {
  data: DashboardVolume | null;
} & ChartNavFilters) {
  const navigate = useNavigate();
  const statuses = data?.series.filter((item) => item.key !== "total") ?? [];
  const plotted = statuses.length > 0 ? statuses : (data?.series ?? []);
  if (!data || data.labels.length === 0 || !hasSeriesData(data.series)) {
    return <EmptyState title={emptyCopy.volume.title} body={emptyCopy.volume.body} />;
  }

  const buckets = volumeBuckets(data);

  function goWeek(index: number | undefined) {
    const bucket = buckets[index ?? -1];
    if (!bucket) {
      return;
    }
    navigate(documentsPath({ from: bucket.from, to: bucket.to, clientId }));
  }

  return (
    <>
      <div className="chart-canvas chart-canvas-wide chart-canvas-clickable" role="img" aria-label="Deed Volume Over Time">
        <Bar
          data={{
            labels: data.labels,
            datasets: plotted.map((series, index) => ({
              label: series.label,
              data: series.data,
              backgroundColor: seriesColor(series.key, series.color, index),
              borderSkipped: false,
              borderRadius: 3,
              maxBarThickness: 44
            }))
          }}
          options={{
            interaction: { mode: "index", intersect: false },
            onHover: pointerOnHit,
            onClick: (event, elements, chart) => goWeek(weekIndex(event, elements, chart)),
            plugins: { legend: { display: false } },
            scales: {
              x: { stacked: true, grid: { display: false } },
              y: { stacked: true, beginAtZero: true, ticks: { precision: 0 }, border: { display: false } }
            }
          }}
        />
      </div>
      <ul className="chart-key" aria-label="Volume series">
        {plotted.map((series, index) => (
          <li key={series.key}>
            <span className="chart-legend-swatch" style={{ background: seriesColor(series.key, series.color, index) }} />
            {series.label}
          </li>
        ))}
      </ul>
      <ul className="chart-legend">
        {buckets.map((bucket) => (
          <li key={`${bucket.from}-${bucket.to}`}>
            <Link
              className="chart-legend-link"
              to={documentsPath({ from: bucket.from, to: bucket.to, clientId })}
              aria-label={`View documents from ${bucket.from} to ${bucket.to}`}
            >
              {bucket.from} – {bucket.to}
            </Link>
          </li>
        ))}
      </ul>
    </>
  );
}

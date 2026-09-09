import type { ActiveElement, ChartEvent } from "chart.js";
import { Bar, Doughnut, Line } from "react-chartjs-2";
import { Link, useNavigate } from "react-router-dom";
import type { DashboardByUser, DashboardStatusMix, DashboardVolume, DashboardUserColumn } from "../api";
import { hasSeriesData, seriesColor } from "../charts";
import { documentsPath } from "../documentsPath";
import EmptyState from "./EmptyState";

const emptyCopy = {
  mix: { title: "No status mix", body: "No deeds in this range. Widen the dates or pick another Client." },
  users: { title: "No by-user activity", body: "No assigned or unassigned deeds in this range." },
  volume: { title: "No volume yet", body: "Upload a PDF or widen the dates to see volume over time." }
};

function pointerOnHit(event: ChartEvent, elements: ActiveElement[]) {
  const canvas = event.native?.target;
  if (canvas instanceof HTMLCanvasElement) {
    canvas.style.cursor = elements.length ? "pointer" : "default";
  }
}

export function StatusMixChart({
  data,
  clientId = ""
}: {
  data: DashboardStatusMix | null;
  clientId?: string;
}) {
  const navigate = useNavigate();
  const slices = data?.series.filter((slice) => slice.count > 0) ?? [];
  if (!data || data.total === 0 || slices.length === 0) {
    return <EmptyState title={emptyCopy.mix.title} body={emptyCopy.mix.body} />;
  }

  function goStatus(status: string) {
    navigate(documentsPath({ status, clientId }));
  }

  return (
    <>
      <div className="chart-canvas chart-canvas-clickable" role="img" aria-label={`Status mix for ${data.total} deeds`}>
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
              to={documentsPath({ status: slice.status, clientId })}
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
  clientId = ""
}: {
  data: DashboardByUser | null;
  clientId?: string;
}) {
  const navigate = useNavigate();
  if (!data || data.labels.length === 0 || !hasSeriesData(data.series)) {
    return <EmptyState title={emptyCopy.users.title} body={emptyCopy.users.body} />;
  }

  function goUser(user: DashboardUserColumn | undefined) {
    if (!user?.userId) {
      return;
    }
    navigate(documentsPath({ assigneeUserId: user.userId, clientId }));
  }

  return (
    <>
      <div className="chart-canvas chart-canvas-clickable" role="img" aria-label="Deeds by user and status">
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
                to={documentsPath({ assigneeUserId: user.userId, clientId })}
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

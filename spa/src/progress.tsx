import { createContext, useContext, useMemo, useState } from "react";

export interface ProgressJob {
  id: string;
  label: string;
  percent: number;
  done: boolean;
  error?: string;
}

interface ProgressState {
  jobs: ProgressJob[];
  start: (id: string, label: string) => void;
  update: (id: string, percent: number) => void;
  finish: (id: string, error?: string) => void;
}

const ProgressContext = createContext<ProgressState | null>(null);

export function ProgressProvider({ children }: { children: React.ReactNode }) {
  const [jobs, setJobs] = useState<ProgressJob[]>([]);

  const value = useMemo<ProgressState>(
    () => ({
      jobs,
      start: (id, label) =>
        setJobs((current) => [...current.filter((job) => job.id !== id), { id, label, percent: 0, done: false }]),
      update: (id, percent) =>
        setJobs((current) => current.map((job) => (job.id === id ? { ...job, percent } : job))),
      finish: (id, error) =>
        setJobs((current) =>
          current.map((job) => (job.id === id ? { ...job, percent: error ? job.percent : 100, done: true, error } : job))
        )
    }),
    [jobs]
  );

  return (
    <ProgressContext.Provider value={value}>
      {children}
      {jobs.length > 0 && (
        <aside className="progress-dock" aria-live="polite">
          {jobs.map((job) => (
            <div key={job.id} className="progress-job">
              <div className="progress-job-meta">
                <strong>{job.label}</strong>
                <span>{job.error ?? (job.done ? "Done" : `${job.percent}%`)}</span>
              </div>
              <progress max={100} value={job.percent} />
            </div>
          ))}
        </aside>
      )}
    </ProgressContext.Provider>
  );
}

export function useProgress() {
  const ctx = useContext(ProgressContext);
  if (!ctx) {
    throw new Error("useProgress must be used within ProgressProvider");
  }
  return ctx;
}

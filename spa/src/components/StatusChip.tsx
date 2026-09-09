const LABELS: Record<string, string> = {
  NeedsReview: "Needs Review",
  Approved: "Approved"
};

export default function StatusChip({ status, title }: { status: string; title?: string | null }) {
  const kind = status.replace(/\s+/g, "").toLowerCase();
  return (
    <span className={`chip chip-${kind}`} data-status={status} title={title ?? undefined}>
      {LABELS[status] ?? status}
    </span>
  );
}

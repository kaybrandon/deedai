const LABELS: Record<string, string> = {
  NeedsReview: "Needs review",
  Approved: "Approved"
};

export default function StatusChip({ status, title }: { status: string; title?: string | null }) {
  const kind = status.replace(/\s+/g, "").toLowerCase();
  return (
    <span className={`chip chip-${kind}`} title={title ?? undefined}>
      {LABELS[status] ?? status}
    </span>
  );
}

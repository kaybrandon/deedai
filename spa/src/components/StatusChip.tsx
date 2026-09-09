export default function StatusChip({ status, title }: { status: string; title?: string | null }) {
  const kind = status.replace(/\s+/g, "").toLowerCase();
  return (
    <span className={`chip chip-${kind}`} title={title ?? undefined}>
      {status === "NeedsReview" ? "Needs review" : status}
    </span>
  );
}

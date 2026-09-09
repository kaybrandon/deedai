export default function StatusChip({ status }: { status: string }) {
  const kind = status.replace(/\s+/g, "").toLowerCase();
  return <span className={`chip chip-${kind}`}>{status === "NeedsReview" ? "Needs review" : status}</span>;
}

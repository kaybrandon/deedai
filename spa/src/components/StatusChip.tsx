const LABELS: Record<string, string> = {
  NeedsReview: "Needs Work",
  Approved: "Complete"
};

export default function StatusChip({
  status,
  title,
  label,
  color
}: {
  status: string;
  title?: string | null;
  label?: string | null;
  color?: string | null;
}) {
  const shown = label || LABELS[status] || status;
  const kind = shown.replace(/\s+/g, "").toLowerCase();
  return (
    <span
      className={`chip chip-${kind}`}
      data-status={status}
      data-label={shown}
      title={title ?? undefined}
      style={color ? { background: color } : undefined}
    >
      {shown}
    </span>
  );
}

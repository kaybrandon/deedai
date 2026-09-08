export default function StatusChip({ status }: { status: string }) {
  const kind = status.toLowerCase();
  return <span className={`chip chip-${kind}`}>{status}</span>;
}

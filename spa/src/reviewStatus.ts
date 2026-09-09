export const NEEDS_REVIEW = "NeedsReview";
export const APPROVED = "Approved";
export const NEEDS_REVIEW_FLAG = "Needs review";

export function displayStatus(row: {
  status: string;
  displayStatus?: string | null;
  reviewStatus?: string | null;
  flags?: Array<{ name: string } | string>;
}): string {
  if (row.displayStatus) {
    return row.displayStatus;
  }

  const needsFlag = (row.flags ?? []).some((flag) =>
    typeof flag === "string" ? flag === NEEDS_REVIEW_FLAG : flag.name === NEEDS_REVIEW_FLAG
  );
  if (needsFlag || row.reviewStatus === NEEDS_REVIEW) {
    return NEEDS_REVIEW;
  }
  if (row.reviewStatus === APPROVED) {
    return APPROVED;
  }
  return row.status;
}

/** ISO date from the API to "9 Oct 2026". */
export const formatDate = (iso: string) =>
  new Date(iso).toLocaleDateString("en-GB", { day: "numeric", month: "short", year: "numeric" });

/** ISO date from the API to "9 Oct 2026, 14:05". */
export const formatDateTime = (iso: string) =>
  new Date(iso).toLocaleString("en-GB", { day: "numeric", month: "short", year: "numeric", hour: "2-digit", minute: "2-digit" });

export const isFuture = (iso: string) => new Date(iso).getTime() > Date.now();

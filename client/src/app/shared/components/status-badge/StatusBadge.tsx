// One colour per status family: green = money held/taken or supplier live, amber = waiting, red = problem.
const tones: Record<string, string> = {
  Enabled: "bg-emerald-50 text-emerald-700 ring-emerald-600/20",
  Authorized: "bg-sky-50 text-sky-700 ring-sky-600/20",
  Captured: "bg-emerald-50 text-emerald-700 ring-emerald-600/20",
  PendingPayment: "bg-amber-50 text-amber-700 ring-amber-600/20",
  PendingVerification: "bg-amber-50 text-amber-700 ring-amber-600/20",
  Invited: "bg-slate-100 text-slate-600 ring-slate-500/20",
  Failed: "bg-rose-50 text-rose-700 ring-rose-600/20",
  Restricted: "bg-rose-50 text-rose-700 ring-rose-600/20",
  CancelledWithFee: "bg-orange-50 text-orange-700 ring-orange-600/20",
  PartiallyRefunded: "bg-violet-50 text-violet-700 ring-violet-600/20",
  Refunded: "bg-violet-50 text-violet-700 ring-violet-600/20",
};

export function StatusBadge({ status }: { status: string }) {
  const tone = tones[status] ?? "bg-slate-100 text-slate-600 ring-slate-500/20";
  return (
    <span className={`inline-flex items-center rounded-full px-2 py-0.5 text-xs font-medium whitespace-nowrap ring-1 ring-inset ${tone}`}>
      {status.replace(/([a-z])([A-Z])/g, "$1 $2")}
    </span>
  );
}

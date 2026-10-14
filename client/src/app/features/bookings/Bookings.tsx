import { Fragment, ReactNode, useState } from "react";
import { bookingService } from "../../core/services/booking.service";
import { StatusBadge } from "../../shared/components/status-badge/StatusBadge";
import { Booking } from "../../shared/models/booking";
import { formatDate, formatDateTime, isFuture } from "../../shared/utils/date";
import { askAmount, money } from "../../shared/utils/money";

// Ops view: who paid what, who keeps what, when it settles, plus the safe actions (complete, cancel, refund).
export function Bookings({ bookings, reload }: { bookings: Booking[]; reload: () => void }) {
  const [open, setOpen] = useState<string>();
  const [error, setError] = useState<string>();

  const act = (fn: () => Promise<unknown>) => {
    setError(undefined);
    fn().then(reload).catch((e: Error) => setError(e.message));
  };
  const withAmount = (label: string, def: number, fn: (a: number) => Promise<unknown>) => {
    const a = askAmount(label, def);
    if (a !== null) act(() => fn(a));
  };

  const currency = bookings[0]?.currency ?? "gbp";
  const sum = (pick: (b: Booking) => number) => bookings.reduce((total, b) => total + pick(b), 0);
  const newestFirst = [...bookings].sort((a, b) => (b.createdAt ?? "").localeCompare(a.createdAt ?? ""));

  return (
    <section className="space-y-4">
      <div className="grid grid-cols-2 gap-3 lg:grid-cols-4">
        <Stat label="Paid by travellers" value={money(sum((b) => b.totalPaid), currency)} hint="Captured minus refunds" />
        <Stat label="Stripe fees" value={money(sum((b) => b.stripeFee), currency)} hint="Paid by Jyrney" />
        <Stat label="Platform net" value={money(sum((b) => b.platformNet), currency)} hint="Commission or fare, after Stripe fee" />
        <Stat label="Supplier net" value={money(sum((b) => b.supplierNet), currency)} hint="Agent bookings, after commission" />
      </div>

      <div className="panel p-0">
        <div className="flex items-center justify-between border-b border-slate-200 px-5 py-4">
          <div>
            <h2 className="text-base font-semibold text-slate-900">Bookings</h2>
            <p className="text-xs text-slate-500">Click a booking ID for the money flow and payment timeline.</p>
          </div>
          <span className="text-xs text-slate-500">{bookings.length} booking{bookings.length === 1 ? "" : "s"}</span>
        </div>

        {error && <p className="mx-5 mt-4 rounded-md bg-rose-50 px-3 py-2 text-rose-700">{error}</p>}

        {bookings.length === 0 ? (
          <p className="px-5 py-12 text-center text-slate-500">No bookings yet. Create one on the Book tab.</p>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full min-w-320 divide-y divide-slate-200">
              <thead className="bg-slate-50">
                <tr>
                  <th className="th">Booking</th>
                  <th className="th">Agent / supplier</th>
                  <th className="th text-right">Quote / held</th>
                  <th className="th text-right">Paid by traveller</th>
                  <th className="th text-right">Stripe fee</th>
                  <th className="th text-right">Commission</th>
                  <th className="th text-right">Platform net</th>
                  <th className="th text-right">Supplier net</th>
                  <th className="th">Settlement</th>
                  <th className="th">Status</th>
                  <th className="th" />
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100">
                {newestFirst.map((b) => {
                  const m = (minor: number) => money(minor, b.currency);
                  const captured = b.capturedAmount > 0;
                  return (
                    <Fragment key={b.id}>
                      <tr className="hover:bg-slate-50/60">
                        <td className="td whitespace-nowrap">
                          <button className="btn-link font-mono" onClick={() => setOpen(open === b.id ? undefined : b.id)}>
                            {b.id.slice(0, 8)}
                          </button>
                          <div className="text-xs text-slate-500">{formatDateTime(b.createdAt)}</div>
                          <div className="mt-1 text-xs text-slate-700">{b.travellerName}</div>
                          <div className="text-xs text-slate-500">paid by {b.payerEmail}</div>
                        </td>
                        <td className="td">
                          <div className="font-medium text-slate-900">{b.model === "Agent" ? b.supplierName ?? "Unknown supplier" : "Jyrney"}</div>
                          <div className="font-mono text-xs text-slate-500">{b.stripeAccountId ?? "platform account"}</div>
                          <span className={`mt-1 inline-block rounded px-1.5 py-0.5 text-[11px] font-medium ${b.model === "Agent" ? "bg-brand-50 text-brand-700" : "bg-slate-100 text-slate-600"}`}>
                            {b.model === "Agent" ? "Agent model" : "Seller model"}
                          </span>
                        </td>
                        <Money>
                          {m(b.quoteAmount)}
                          <Sub>held {m(b.authorizedAmount)}</Sub>
                        </Money>
                        <Money>
                          {captured ? <span className="font-semibold text-slate-900">{m(b.totalPaid)}</span> : <Dash />}
                          {b.refundedAmount > 0 && <Sub>captured {m(b.capturedAmount)} · refunded {m(b.refundedAmount)}</Sub>}
                        </Money>
                        <Money>{captured && b.settlementDate ? <span className="text-rose-600">−{m(b.stripeFee)}</span> : <Dash />}</Money>
                        <Money>
                          {b.model === "Agent" && captured ? m(b.netCommission) : <Dash />}
                          {b.model === "Agent" && <Sub>{b.commissionBps / 100}%{b.applicationFeeRefunded > 0 ? ` · ${m(b.applicationFeeRefunded)} returned` : ""}</Sub>}
                        </Money>
                        <Money>{captured ? <Signed value={b.platformNet} text={m(b.platformNet)} /> : <Dash />}</Money>
                        <Money>{b.model === "Agent" && captured ? <span className="font-medium text-slate-900">{m(b.supplierNet)}</span> : <Dash />}</Money>
                        <td className="td whitespace-nowrap">
                          {!captured ? (
                            <Dash />
                          ) : b.settlementDate ? (
                            <>
                              <div className="text-slate-900">{formatDate(b.settlementDate)}</div>
                              <div className={`text-xs ${isFuture(b.settlementDate) ? "text-amber-600" : "text-emerald-600"}`}>
                                {isFuture(b.settlementDate) ? "pending" : "available"}
                              </div>
                            </>
                          ) : (
                            <span className="text-xs text-amber-600">awaiting Stripe · Sync</span>
                          )}
                        </td>
                        <td className="td">
                          <StatusBadge status={b.status} />
                          {b.lastError && <div className="mt-1 max-w-48 text-xs text-rose-600">{b.lastError}</div>}
                        </td>
                        <td className="td">
                          <div className="flex flex-wrap justify-end gap-1.5">
                            {b.status === "Authorized" && (
                              <button className="btn btn-sm btn-primary" onClick={() => withAmount("Final price (£) from supplier", b.quoteAmount, (a) => bookingService.complete(b.id, a))}>
                                Complete trip
                              </button>
                            )}
                            {["PendingPayment", "Authorized", "Captured"].includes(b.status) && (
                              <button className="btn btn-sm" onClick={() => withAmount("Cancellation fee (£), 0 = free", 0, (a) => bookingService.cancel(b.id, a))}>Cancel</button>
                            )}
                            {["Captured", "CancelledWithFee", "PartiallyRefunded"].includes(b.status) && (
                              <button className="btn btn-sm" onClick={() => withAmount("Refund amount (£)", b.capturedAmount - b.refundedAmount, (a) => bookingService.refund(b.id, a))}>Refund</button>
                            )}
                            <button className="btn btn-sm" onClick={() => act(() => bookingService.syncBooking(b.id))}>Sync</button>
                          </div>
                        </td>
                      </tr>
                      {open === b.id && (
                        <tr className="bg-slate-50">
                          <td colSpan={11} className="px-5 py-4">
                            <Details booking={b} />
                          </td>
                        </tr>
                      )}
                    </Fragment>
                  );
                })}
              </tbody>
            </table>
          </div>
        )}
      </div>
    </section>
  );
}

// Expanded row: where the traveller's money went, and the payment timeline.
function Details({ booking: b }: { booking: Booking }) {
  const m = (minor: number) => money(minor, b.currency);
  const flow: [string, string, string?][] = [
    ["Paid by traveller", m(b.totalPaid), b.refundedAmount > 0 ? `${m(b.capturedAmount)} captured − ${m(b.refundedAmount)} refunded` : undefined],
    ...(b.model === "Agent"
      ? ([
          ["Supplier keeps", m(b.supplierNet), `transferred to ${b.stripeAccountId}`],
          ["Jyrney commission", m(b.netCommission), `${b.commissionBps / 100}% application fee`],
        ] as [string, string, string?][])
      : []),
    ["Stripe fee", `−${m(b.stripeFee)}`, "paid by Jyrney"],
    ["Jyrney net", m(b.platformNet), b.model === "Agent" ? "commission − Stripe fee" : "fare − Stripe fee"],
  ];

  return (
    <div className="grid gap-6 md:grid-cols-[minmax(0,320px)_1fr]">
      <div>
        <h3 className="mb-2 text-xs font-semibold tracking-wide text-slate-500 uppercase">Money flow</h3>
        <dl className="divide-y divide-slate-200 rounded-lg bg-white ring-1 ring-slate-200">
          {flow.map(([label, value, note]) => (
            <div key={label} className="flex items-baseline justify-between gap-4 px-3 py-2">
              <dt>
                <div className="text-slate-700">{label}</div>
                {note && <div className="text-xs text-slate-400">{note}</div>}
              </dt>
              <dd className="font-medium whitespace-nowrap text-slate-900 tabular-nums">{value}</dd>
            </div>
          ))}
        </dl>
      </div>
      <div>
        <h3 className="mb-2 text-xs font-semibold tracking-wide text-slate-500 uppercase">Timeline (UTC)</h3>
        <p className="mb-2 font-mono text-xs text-slate-500">{b.paymentIntentId}</p>
        <ol className="space-y-1.5 border-l-2 border-brand-100 pl-4">
          {b.timeline.map((t, i) => (
            <li key={i} className="text-slate-700">{t}</li>
          ))}
        </ol>
      </div>
    </div>
  );
}

function Stat({ label, value, hint }: { label: string; value: string; hint: string }) {
  return (
    <div className="panel p-4">
      <div className="text-xs font-medium text-slate-500">{label}</div>
      <div className="mt-1 text-xl font-semibold text-slate-900 tabular-nums">{value}</div>
      <div className="mt-0.5 text-xs text-slate-400">{hint}</div>
    </div>
  );
}

const Money = ({ children }: { children: ReactNode }) => <td className="td text-right whitespace-nowrap tabular-nums">{children}</td>;
const Sub = ({ children }: { children: ReactNode }) => <div className="text-xs text-slate-500">{children}</div>;
const Dash = () => <span className="text-slate-300">—</span>;
const Signed = ({ value, text }: { value: number; text: string }) => (
  <span className={`font-medium ${value < 0 ? "text-rose-600" : "text-emerald-700"}`}>{text}</span>
);

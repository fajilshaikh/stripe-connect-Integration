import { FormEvent, useEffect, useState } from "react";
import { supplierService } from "../../core/services/supplier.service";
import { StatusBadge } from "../../shared/components/status-badge/StatusBadge";
import { Supplier } from "../../shared/models/supplier";

// Ops screen: Jyrney (the platform) creates supplier accounts and sends them the hosted onboarding link.
export function Suppliers({ suppliers, reload }: { suppliers: Supplier[]; reload: () => void }) {
  const [name, setName] = useState("");
  const [email, setEmail] = useState("");
  const [links, setLinks] = useState<Record<string, string>>({});
  const [error, setError] = useState<string>();

  const run = (fn: () => Promise<unknown>) => fn().then(reload).catch((e: Error) => setError(e.message));

  // Supplier returns from Stripe onboarding with ?onboarded=<id>: pull fresh status.
  useEffect(() => {
    const id = new URLSearchParams(window.location.search).get("onboarded");
    if (id) run(() => supplierService.syncSupplier(id)).then(() => window.history.replaceState(null, "", "/"));
  }, []);

  function create(e: FormEvent) {
    e.preventDefault();
    setError(undefined);
    run(() => supplierService.createSupplier({ name, email })).then(() => (setName(""), setEmail("")));
  }

  function getOnboardingLink(id: string) {
    supplierService
      .getOnboardingLink(id)
      .then((r) => setLinks((l) => ({ ...l, [id]: r.url })))
      .catch((e: Error) => setError(e.message));
  }

  return (
    <section className="panel p-0">
      <div className="border-b border-slate-200 px-5 py-4">
        <h2 className="text-base font-semibold text-slate-900">Suppliers (Stripe Connect)</h2>
        <p className="text-xs text-slate-500">Create an Express account, send the onboarding link, and track verification.</p>
        <form onSubmit={create} className="mt-4 flex flex-wrap gap-2">
          <input className="field w-56" placeholder="Supplier name" value={name} onChange={(e) => setName(e.target.value)} required />
          <input className="field w-64" placeholder="Email" type="email" value={email} onChange={(e) => setEmail(e.target.value)} required />
          <button className="btn btn-primary">Create connected account</button>
          <button className="btn" type="button" onClick={() => run(supplierService.importSuppliers)}>Re-load from Stripe</button>
        </form>
      </div>

      {error && <p className="mx-5 mt-4 rounded-md bg-rose-50 px-3 py-2 text-rose-700">{error}</p>}

      <div className="overflow-x-auto">
        <table className="w-full min-w-225 divide-y divide-slate-200">
          <thead className="bg-slate-50">
            <tr>
              <th className="th">Name</th>
              <th className="th">Account</th>
              <th className="th">Status</th>
              <th className="th text-center">Charges</th>
              <th className="th text-center">Payouts</th>
              <th className="th">Requirements due</th>
              <th className="th" />
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-100">
            {suppliers.map((s) => (
              <tr key={s.id} className="hover:bg-slate-50/60">
                <td className="td">
                  <div className="font-medium text-slate-900">{s.name}</div>
                  <div className="text-xs text-slate-500">{s.email}</div>
                </td>
                <td className="td font-mono text-xs text-slate-600">{s.stripeAccountId}</td>
                <td className="td">
                  <StatusBadge status={s.status} />
                  {s.disabledReason && <div className="mt-1 text-xs text-slate-500">{s.disabledReason}</div>}
                </td>
                <td className="td text-center">{s.chargesEnabled ? <Yes /> : <No />}</td>
                <td className="td text-center">{s.payoutsEnabled ? <Yes /> : <No />}</td>
                <td className="td text-xs text-slate-500">{s.currentlyDue.length ? s.currentlyDue.join(", ") : "none"}</td>
                <td className="td">
                  <div className="flex flex-wrap items-center justify-end gap-1.5">
                    <button className="btn btn-sm" onClick={() => getOnboardingLink(s.id)}>Onboarding link</button>
                    <button className="btn btn-sm" onClick={() => run(() => supplierService.syncSupplier(s.id))}>Refresh</button>
                    {links[s.id] && (
                      <a className="btn-link text-xs" href={links[s.id]} target="_blank" rel="noreferrer">Open / send to supplier ↗</a>
                    )}
                  </div>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </section>
  );
}

const Yes = () => <span className="font-semibold text-emerald-600">✓</span>;
const No = () => <span className="text-slate-300">✕</span>;

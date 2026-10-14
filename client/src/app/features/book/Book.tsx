import { FormEvent, ReactNode, useState } from "react";
import type { Appearance } from "@stripe/stripe-js";
import { bookingService } from "../../core/services/booking.service";
import { PaymentForm } from "../../shared/components/payment-form/PaymentForm";
import { CommercialModel, CreatedBooking } from "../../shared/models/booking";
import { Supplier } from "../../shared/models/supplier";
import { money } from "../../shared/utils/money";

type Props = { publishableKey: string; suppliers: Supplier[]; appearance: Appearance; onDone: () => void };

export function Book({ publishableKey, suppliers, appearance, onDone }: Props) {
  const [model, setModel] = useState<CommercialModel>("Agent");
  const [supplierId, setSupplierId] = useState("");
  const [payerEmail, setPayerEmail] = useState("booker@acme-corp.test");
  const [travellerName, setTravellerName] = useState("Jane Traveller");
  const [quote, setQuote] = useState("45.00");
  const [commission, setCommission] = useState("15");
  const [pending, setPending] = useState<CreatedBooking>();
  const [message, setMessage] = useState<string>();

  const payable = suppliers.filter((s) => s.isPayable);

  async function create(e: FormEvent) {
    e.preventDefault();
    setMessage(undefined);
    try {
      setPending(
        await bookingService.createBooking({
          model,
          supplierId: model === "Agent" ? supplierId : undefined,
          payerEmail,
          travellerName,
          quoteAmount: Math.round(parseFloat(quote) * 100),
          commissionBps: Math.round(parseFloat(commission) * 100),
        })
      );
    } catch (err) {
      setMessage((err as Error).message);
    }
  }

  async function authorized() {
    const b = await bookingService.syncBooking(pending!.booking.id); // webhook will also do this
    setMessage(`Booking ${b.id.slice(0, 8)} is ${b.status}: ${money(b.authorizedAmount, b.currency)} held on the card.`);
    setPending(undefined);
    onDone();
  }

  if (pending)
    return (
      <section className="panel max-w-xl">
        <h2 className="text-base font-semibold text-slate-900">Payment for {pending.booking.travellerName}</h2>
        <p className="mt-1 mb-4 text-slate-600">
          Quote {money(pending.booking.quoteAmount)} · holding <strong>{money(pending.booking.authorizedAmount)}</strong> (includes price-variance buffer) ·{" "}
          {pending.booking.stripeAccountId ? `destination charge to ${pending.booking.stripeAccountId}` : "charged by Jyrney (seller)"}
        </p>
        <PaymentForm
          publishableKey={publishableKey}
          clientSecret={pending.clientSecret}
          appearance={appearance}
          onAuthorized={authorized}
        />
        <p className="mt-4 text-xs text-slate-500">Test cards: 4242 4242 4242 4242 · 3DS 4000 0025 0000 3155 · decline 4000 0000 0000 9995</p>
        <button className="btn-link mt-3 text-sm" onClick={() => setPending(undefined)}>← Back</button>
      </section>
    );

  return (
    <section className="panel max-w-xl">
      <h2 className="text-base font-semibold text-slate-900">New booking</h2>
      <p className="mb-5 text-xs text-slate-500">The fare plus a buffer is held on the card; the final price is captured when the trip completes.</p>
      <form onSubmit={create} className="grid gap-4">
        <Field label="Commercial model">
          <select className="field" value={model} onChange={(e) => setModel(e.target.value as CommercialModel)}>
            <option value="Agent">Agent: supplier is merchant, Jyrney takes commission</option>
            <option value="Seller">Seller: Jyrney is merchant, full fare</option>
          </select>
        </Field>
        {model === "Agent" && (
          <div className="grid gap-4 sm:grid-cols-[1fr_8rem]">
            <Field label="Supplier (payable only)">
              <select className="field" value={supplierId} onChange={(e) => setSupplierId(e.target.value)} required>
                <option value="">{payable.length ? "Select…" : "No onboarded suppliers yet"}</option>
                {payable.map((s) => <option key={s.id} value={s.id}>{s.name}</option>)}
              </select>
            </Field>
            <Field label="Commission %">
              <input className="field" value={commission} onChange={(e) => setCommission(e.target.value)} />
            </Field>
          </div>
        )}
        <Field label="Payer email (may differ from traveller)">
          <input className="field" type="email" value={payerEmail} onChange={(e) => setPayerEmail(e.target.value)} required />
        </Field>
        <div className="grid gap-4 sm:grid-cols-[1fr_8rem]">
          <Field label="Traveller">
            <input className="field" value={travellerName} onChange={(e) => setTravellerName(e.target.value)} required />
          </Field>
          <Field label="Quoted fare (£)">
            <input className="field" value={quote} onChange={(e) => setQuote(e.target.value)} required />
          </Field>
        </div>
        <button className="btn btn-primary justify-self-start">Continue to payment</button>
      </form>
      {message && <p className="mt-4 rounded-md bg-brand-50 px-3 py-2 text-brand-700">{message}</p>}
    </section>
  );
}

function Field({ label, children }: { label: string; children: ReactNode }) {
  return (
    <label className="grid gap-1 text-xs font-medium text-slate-600">
      {label}
      {children}
    </label>
  );
}

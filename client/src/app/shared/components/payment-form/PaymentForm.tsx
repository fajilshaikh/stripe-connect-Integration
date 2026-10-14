// Shared payments component for the main app and white-label builds.
// Card data goes browser -> Stripe (Payment Element iframes); Jyrney never sees it (PCI SAQ-A).
import { FormEvent, useState } from "react";
import type { Appearance } from "@stripe/stripe-js";
import { Elements, PaymentElement, useElements, useStripe } from "@stripe/react-stripe-js";
import { getStripe } from "../../../core/services/stripe.service";

type Props = {
  publishableKey: string;
  clientSecret: string;
  stripeAccount?: string;
  appearance?: Appearance;
  onAuthorized: () => void;
};

export function PaymentForm({ publishableKey, clientSecret, stripeAccount, appearance, onAuthorized }: Props) {
  return (
    <Elements stripe={getStripe(publishableKey, stripeAccount)} options={{ clientSecret, appearance }}>
      <CardForm onAuthorized={onAuthorized} />
    </Elements>
  );
}

function CardForm({ onAuthorized }: { onAuthorized: () => void }) {
  const stripe = useStripe();
  const elements = useElements();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string>();

  async function submit(e: FormEvent) {
    e.preventDefault();
    if (!stripe || !elements) return;
    setBusy(true);
    setError(undefined);
    // Handles 3DS/SCA in a modal; manual capture means a successful result is "requires_capture".
    const { error, paymentIntent } = await stripe.confirmPayment({
      elements,
      redirect: "if_required",
      confirmParams: { return_url: window.location.href },
    });
    setBusy(false);
    if (error) setError(error.message);
    else if (paymentIntent?.status === "requires_capture") onAuthorized();
  }

  return (
    <form onSubmit={submit} className="grid gap-4">
      <PaymentElement />
      <button className="btn btn-primary justify-self-start" disabled={!stripe || busy}>{busy ? "Authorising…" : "Authorise payment"}</button>
      {error && <p className="rounded-md bg-rose-50 px-3 py-2 text-rose-700">{error}</p>}
    </form>
  );
}

import { loadStripe, Stripe } from "@stripe/stripe-js";

const instances = new Map<string, Promise<Stripe | null>>();

// Seller and destination (Agent) charges live on the platform, so `stripeAccount` is omitted.
// It is only needed if a tenant ever uses direct charges on a connected account.
export function getStripe(publishableKey: string, stripeAccount?: string) {
  const key = stripeAccount ?? "platform";
  if (!instances.has(key)) instances.set(key, loadStripe(publishableKey, stripeAccount ? { stripeAccount } : undefined));
  return instances.get(key)!;
}

import type { Appearance } from "@stripe/stripe-js";

export const tabs = ["suppliers", "book", "bookings"] as const;
export type Tab = (typeof tabs)[number];

// White-label tenants differ by branding only; Elements Appearance API themes the card form.
export const tenants: Record<string, Appearance> = {
  Jyrney: { theme: "stripe", variables: { colorPrimary: "#5b3df5" } },
  "Acme Travel (white-label)": { theme: "night", variables: { colorPrimary: "#ff7a00", borderRadius: "2px" } },
};

/** Minor units (pence) to a formatted amount, e.g. 4500 -> "£45.00". */
export const money = (minor: number, currency = "gbp") =>
  new Intl.NumberFormat("en-GB", { style: "currency", currency }).format(minor / 100);

/** Prompts for an amount in major units and returns minor units, or null if cancelled. */
export const askAmount = (label: string, defaultMinor: number) => {
  const v = window.prompt(label, (defaultMinor / 100).toFixed(2));
  return v === null ? null : Math.round(parseFloat(v) * 100);
};

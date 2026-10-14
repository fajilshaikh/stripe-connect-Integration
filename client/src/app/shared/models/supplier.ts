export type OnboardingStatus = "Invited" | "PendingVerification" | "Enabled" | "Restricted";

export type Supplier = {
  id: string;
  name: string;
  email: string;
  stripeAccountId: string;
  status: OnboardingStatus;
  chargesEnabled: boolean;
  payoutsEnabled: boolean;
  disabledReason?: string;
  currentlyDue: string[];
  isPayable: boolean;
};

export type SupplierToCreate = {
  name: string;
  email: string;
};

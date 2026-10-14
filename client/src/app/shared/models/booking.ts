export type CommercialModel = "Seller" | "Agent";

// Amounts are in minor units (pence).
export type Booking = {
  id: string;
  createdAt: string;
  model: CommercialModel;
  supplierId?: string;
  supplierName?: string;
  stripeAccountId?: string;
  payerEmail: string;
  travellerName: string;
  currency: string;
  quoteAmount: number;
  authorizedAmount: number;
  capturedAmount: number;
  refundedAmount: number;
  applicationFee: number;
  applicationFeeRefunded: number;
  commissionBps: number;
  stripeFee: number;
  settlementDate?: string;
  totalPaid: number; // captured - refunded
  netCommission: number; // commission kept after refunds
  platformNet: number; // after the Stripe fee
  supplierNet: number;
  paymentIntentId: string;
  status: string;
  lastError?: string;
  timeline: string[];
};

export type BookingToCreate = {
  model: CommercialModel;
  supplierId?: string;
  payerEmail: string;
  travellerName: string;
  quoteAmount: number;
  commissionBps?: number;
};

export type CreatedBooking = {
  booking: Booking;
  clientSecret: string;
};

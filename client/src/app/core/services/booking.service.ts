import { Booking, BookingToCreate, CreatedBooking } from "../../shared/models/booking";
import { http } from "./http";

// Amounts are in minor units (pence).
export const bookingService = {
  getBookings: () => http.get<Booking[]>("/bookings"),
  createBooking: (booking: BookingToCreate) => http.post<CreatedBooking>("/bookings", booking),
  syncBooking: (id: string) => http.post<Booking>(`/bookings/${id}/sync`),
  complete: (id: string, amount: number) => http.post<Booking>(`/bookings/${id}/complete`, { amount }),
  cancel: (id: string, amount: number) => http.post<Booking>(`/bookings/${id}/cancel`, { amount }),
  refund: (id: string, amount: number) => http.post<Booking>(`/bookings/${id}/refund`, { amount }),
};

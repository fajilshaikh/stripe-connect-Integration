import { Supplier, SupplierToCreate } from "../../shared/models/supplier";
import { http } from "./http";

export const supplierService = {
  getSuppliers: () => http.get<Supplier[]>("/suppliers"),
  createSupplier: (supplier: SupplierToCreate) => http.post<Supplier>("/suppliers", supplier),
  importSuppliers: () => http.post<Supplier[]>("/suppliers/import"),
  getOnboardingLink: (id: string) => http.post<{ url: string }>(`/suppliers/${id}/onboarding-link`),
  syncSupplier: (id: string) => http.post<Supplier>(`/suppliers/${id}/sync`),
};

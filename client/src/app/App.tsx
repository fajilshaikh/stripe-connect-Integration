import { useCallback, useEffect, useState } from "react";
import { Tab, tenants } from "./app.config";
import { bookingService } from "./core/services/booking.service";
import { configService } from "./core/services/config.service";
import { supplierService } from "./core/services/supplier.service";
import { Book } from "./features/book/Book";
import { Bookings } from "./features/bookings/Bookings";
import { Suppliers } from "./features/suppliers/Suppliers";
import { Header } from "./layout/header/Header";
import { Booking } from "./shared/models/booking";
import { Supplier } from "./shared/models/supplier";

export function App() {
  const [tab, setTab] = useState<Tab>("suppliers");
  const [tenant, setTenant] = useState("Jyrney");
  const [publishableKey, setPublishableKey] = useState("");
  const [suppliers, setSuppliers] = useState<Supplier[]>([]);
  const [bookings, setBookings] = useState<Booking[]>([]);

  const reload = useCallback(() => {
    supplierService.getSuppliers().then(setSuppliers);
    bookingService.getBookings().then(setBookings);
  }, []);

  useEffect(() => {
    configService.getConfig().then((c) => setPublishableKey(c.publishableKey));
    reload();
  }, [reload]);

  return (
    <main className="mx-auto max-w-screen-2xl px-4 py-6 sm:px-6">
      <Header
        tenant={tenant}
        tenantNames={Object.keys(tenants)}
        onTenantChange={setTenant}
        tab={tab}
        onTabChange={(t) => (setTab(t), reload())}
      />
      {tab === "suppliers" && <Suppliers suppliers={suppliers} reload={reload} />}
      {tab === "book" && publishableKey && (
        <Book publishableKey={publishableKey} suppliers={suppliers} appearance={tenants[tenant]} onDone={() => (reload(), setTab("bookings"))} />
      )}
      {tab === "bookings" && <Bookings bookings={bookings} reload={reload} />}
    </main>
  );
}

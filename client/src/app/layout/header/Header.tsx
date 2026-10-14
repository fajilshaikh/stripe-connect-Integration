import { Tab, tabs } from "../../app.config";

type Props = {
  tenant: string;
  tenantNames: string[];
  onTenantChange: (tenant: string) => void;
  tab: Tab;
  onTabChange: (tab: Tab) => void;
};

export function Header({ tenant, tenantNames, onTenantChange, tab, onTabChange }: Props) {
  return (
    <header className="mb-6">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div className="flex items-center gap-3">
          <span className="grid size-9 place-items-center rounded-lg bg-brand-600 font-bold text-white">J</span>
          <div>
            <h1 className="text-lg font-semibold text-slate-900">Jyrney</h1>
            <p className="text-xs text-slate-500">Stripe Connect demo</p>
          </div>
        </div>
        <label className="flex items-center gap-2 text-xs text-slate-500">
          Tenant
          <select className="field w-auto" value={tenant} onChange={(e) => onTenantChange(e.target.value)}>
            {tenantNames.map((t) => <option key={t}>{t}</option>)}
          </select>
        </label>
      </div>
      <nav className="mt-5 flex gap-1 border-b border-slate-200">
        {tabs.map((t) => (
          <button
            key={t}
            onClick={() => onTabChange(t)}
            className={`-mb-px cursor-pointer border-b-2 px-4 py-2 text-sm font-medium capitalize transition ${
              tab === t ? "border-brand-600 text-brand-700" : "border-transparent text-slate-500 hover:text-slate-800"
            }`}
          >
            {t}
          </button>
        ))}
      </nav>
    </header>
  );
}

import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { useSearchParams } from "react-router-dom";
import { apiClient } from "@/lib/api/apiClient";
import { useCurrentOrganization } from "@/features/organizations/hooks/useCurrentOrganization";
import { permissions } from "@/features/authorization/permissions";

export interface AuditRecord {
  id: string; organizationId: string; actorUserId: string | null; action: string;
  resourceType: string; resourceId: string | null; ipAddress: string | null;
  userAgent: string | null; timestamp: string; metadata: Record<string, string | null>;
}

const actions = ["MEMBER_INVITED", "INVITATION_ACCEPTED", "INVITATION_REVOKED", "MEMBER_ADDED", "MEMBER_REMOVED", "ROLE_CHANGED", "PROJECT_MEMBER_ADDED", "PROJECT_MEMBER_REMOVED", "PROJECT_ARCHIVED", "WORK_ITEM_CREATED", "WORK_ITEM_UPDATED", "WORK_ITEM_ARCHIVED", "WORK_ITEM_ASSIGNED", "WORK_ITEM_UNASSIGNED", "WORK_ITEM_TRANSITIONED", "ATTACHMENT_UPLOADED", "ATTACHMENT_DELETED", "ATTACHMENT_DOWNLOADED"];
const resourceTypes = ["OrganizationInvitation", "OrganizationMember", "ProjectMember", "Project", "WorkItem", "Attachment"];
const fields = ["action", "actorUserId", "resourceType", "resourceId", "from", "to", "page", "pageSize"];

export function AuditPage() {
  const organization = useCurrentOrganization();
  const [params, setParams] = useSearchParams();
  const [formError, setFormError] = useState<string | null>(null);
  const queryParams = new URLSearchParams();
  fields.forEach(name => { const value = params.get(name); if (value) queryParams.set(name, value); });
  const queryString = queryParams.toString();
  const allowed = organization.permissions.includes(permissions.auditView);
  const query = useQuery({
    queryKey: ["audit-logs", organization.id, queryString],
    queryFn: () => apiClient.get<AuditRecord[]>(`/organizations/${organization.id}/audit-logs?${queryString}`),
    enabled: allowed,
  });
  const parsedPage = Number(params.get("page") ?? 1);
  const page = Number.isInteger(parsedPage) && parsedPage >= 1 && parsedPage <= 100000 ? parsedPage : 1;
  const pageSize = Number(params.get("pageSize") ?? 25);
  const setPage = (value: number) => setParams(current => { const next = new URLSearchParams(current); next.set("page", String(value)); return next; });
  const inputClass = "mt-1 block w-full rounded border border-slate-300 bg-white px-2 py-2 text-sm";
  if (!allowed) return <p role="alert">Audit kayıtlarını görüntüleme yetkiniz yok.</p>;
  return <section>
    <h1 className="text-2xl font-semibold">Audit kayıtları</h1>
    <p className="mt-2 text-sm text-slate-600">{organization.name} için salt okunur güvenlik işlem geçmişi. Hesap güvenliği kayıtları bu ekranda gösterilmez.</p>
    <form key={queryString} aria-label="Audit filtreleri" className="mt-5 rounded-lg border bg-white p-4" onSubmit={event => {
      event.preventDefault();
      const values = Object.fromEntries(new FormData(event.currentTarget)) as Record<string, string>;
      if (values.from && values.to && values.from > values.to) { setFormError("Başlangıç tarihi bitişten sonra olamaz."); return; }
      setFormError(null);
      setParams(current => {
        const next = new URLSearchParams(current);
        for (const [key, value] of Object.entries(values)) { if (value) next.set(key, value); else next.delete(key); }
        next.delete("page"); return next;
      });
    }}>
      <div className="grid gap-3 text-sm sm:grid-cols-2 lg:grid-cols-3">
        <label>İşlem<select name="action" defaultValue={params.get("action") ?? ""} className={inputClass}><option value="">Tümü</option>{actions.map(action => <option key={action}>{action}</option>)}</select></label>
        <label>Kaynak türü<select name="resourceType" defaultValue={params.get("resourceType") ?? ""} className={inputClass}><option value="">Tümü</option>{resourceTypes.map(type => <option key={type}>{type}</option>)}</select></label>
        {([["actorUserId", "Aktör kullanıcı ID"], ["resourceId", "Kaynak ID"]] as const).map(([name, title]) => <label key={name}>{title}<input name={name} defaultValue={params.get(name) ?? ""} pattern="[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}" className={inputClass} /></label>)}
        <label>Başlangıç (UTC)<input name="from" type="date" defaultValue={params.get("from") ?? ""} className={inputClass} /></label>
        <label>Bitiş (UTC)<input name="to" type="date" defaultValue={params.get("to") ?? ""} className={inputClass} /></label>
        <label>Sayfa boyutu<select name="pageSize" defaultValue={params.get("pageSize") ?? "25"} className={inputClass}>{[10, 25, 50, 100].map(size => <option key={size}>{size}</option>)}</select></label>
      </div>
      {formError ? <p role="alert" className="mt-2 text-red-700">{formError}</p> : null}
      <div className="mt-3 flex gap-3"><button className="rounded bg-slate-900 px-3 py-2 text-sm text-white">Filtreleri uygula</button><button type="button" onClick={() => { setFormError(null); setParams(current => { const next = new URLSearchParams(current); fields.forEach(name => next.delete(name)); return next; }); }} className="rounded border px-3 py-2 text-sm">Temizle</button></div>
    </form>
    <div className="my-4 flex items-center gap-3 text-sm"><button disabled={page <= 1 || query.isFetching} onClick={() => setPage(page - 1)} className="rounded border px-3 py-2 disabled:opacity-40">Önceki sayfa</button><span>Sayfa {page}</span><button disabled={page >= 100000 || !Number.isInteger(pageSize) || pageSize < 1 || query.isFetching || query.isError || (query.data?.length ?? 0) < pageSize} onClick={() => setPage(page + 1)} className="rounded border px-3 py-2 disabled:opacity-40">Sonraki sayfa</button><button disabled={query.isFetching} onClick={() => void query.refetch()} className="rounded border px-3 py-2">Yenile</button></div>
    {query.isLoading ? <p role="status">Audit kayıtları yükleniyor...</p> : null}
    {query.isError ? <p role="alert" className="rounded bg-red-50 p-3 text-red-700">Audit kayıtları yüklenemedi. Yetkinizi ve filtreleri kontrol edip yeniden deneyin.</p> : null}
    {!query.isError && query.data?.length === 0 ? <p className="rounded border border-dashed p-6 text-center">Bu filtrelerde veya sayfada audit kaydı yok.</p> : null}
    {!query.isError && query.data?.length ? <div className="overflow-x-auto rounded border bg-white"><table className="w-full text-left text-sm"><thead className="bg-slate-100"><tr>{["Zaman", "İşlem", "Aktör", "Kaynak", "Bağlantı / Metadata"].map(title => <th key={title} className="p-3">{title}</th>)}</tr></thead><tbody>{query.data.map(log => <tr key={log.id} className="border-t align-top">
      <td className="whitespace-nowrap p-3"><time dateTime={log.timestamp}>{new Date(log.timestamp).toLocaleString()}</time></td><td className="p-3 font-medium">{log.action}</td><td className="break-all p-3">{log.actorUserId ?? "Doğrulanmış aktör yok / sistem"}</td><td className="break-all p-3">{log.resourceType}<br />{log.resourceId ?? "—"}</td><td className="p-3"><details><summary>Detay</summary><p className="break-all">IP: {log.ipAddress ?? "—"}</p><p className="max-w-md break-all">User-Agent: {log.userAgent ?? "—"}</p><pre className="max-w-md whitespace-pre-wrap break-all">{JSON.stringify(log.metadata, null, 2)}</pre></details></td>
    </tr>)}</tbody></table></div> : null}
  </section>;
}

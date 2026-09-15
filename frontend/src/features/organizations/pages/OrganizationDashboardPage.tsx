import { useQuery } from "@tanstack/react-query";
import { Link, useSearchParams } from "react-router-dom";
import { apiClient } from "@/lib/api/apiClient";
import { useCurrentUser } from "@/features/auth/hooks/useCurrentUser";
import { useCurrentOrganization } from "../hooks/useCurrentOrganization";
import type { Dashboard } from "../api/dashboard";

export function OrganizationDashboardPage() {
  const organization = useCurrentOrganization();
  const currentUser = useCurrentUser();
  const [params, setParams] = useSearchParams();
  const requestedPage = Number(params.get("myTasksPage") ?? 1);
  const page = Number.isInteger(requestedPage) && requestedPage >= 1 && requestedPage <= 100000 ? requestedPage : 1;
  const query = useQuery({
    queryKey: ["dashboard", organization.id, currentUser.data?.id, page],
    queryFn: () => apiClient.get<Dashboard>(`/organizations/${organization.id}/dashboard?myTasksPage=${page}`),
    enabled: Boolean(currentUser.data?.id),
    staleTime: 0,
    refetchOnMount: "always",
    refetchOnWindowFocus: "always",
    refetchInterval: 30_000,
    retry: false,
  });
  const setPage = (value: number) => setParams(previous => {
    const next = new URLSearchParams(previous);
    next.set("myTasksPage", String(value));
    return next;
  });
  const data = query.isError || currentUser.isError ? undefined : query.data;
  const total = data?.tasksByState.reduce((sum, state) => sum + state.count, 0) ?? 0;
  const workItemUrl = (key: string) => `/app/${organization.slug}/work-items/${encodeURIComponent(key)}`;
  const buttonClass = "rounded-md border border-slate-300 bg-white px-3 py-2 text-sm disabled:opacity-40";

  return <section className="space-y-6">
    <div className="flex flex-wrap items-start justify-between gap-3">
      <div><p className="text-sm font-medium text-slate-500">Dashboard</p><h1 className="mt-1 text-2xl font-semibold text-slate-900">{organization.name}</h1>
        <p className="mt-2 max-w-2xl text-sm text-slate-600">Yalnız erişebildiğiniz aktif projeler ve arşivlenmemiş işler. Tarihler UTC; yaklaşan işler bugün dahil 7 gün içindedir.</p></div>
      <button className={buttonClass} disabled={query.isFetching || !currentUser.data} onClick={() => void query.refetch()}>Yenile</button>
    </div>
    {query.isLoading || currentUser.isLoading ? <p role="status">Dashboard yükleniyor...</p> : null}
    {query.isError || currentUser.isError ? <p role="alert" className="rounded-lg bg-red-50 p-4 text-red-700">Dashboard yüklenemedi. Erişiminizi kontrol edip yeniden deneyin.</p> : null}
    {data ? <>
      <p className="text-xs text-slate-500">UTC günü: {data.today} · Veriler 30 saniyede bir yeniden kontrol edilir.</p>
      <dl className="grid grid-cols-2 gap-3 lg:grid-cols-5">
        {([["Aktif projeler", data.activeProjects], ["Bana atanan açık işler", data.myWorkItems], ["Geciken", data.overdue], ["Yaklaşan (7 gün)", data.dueSoon], ["Kritik açık işler", data.critical]] as const).map(([label, value]) =>
          <div key={label} className="rounded-xl border border-slate-200 bg-white p-4"><dt className="text-sm text-slate-600">{label}</dt><dd className="mt-2 text-3xl font-semibold tabular-nums text-slate-900">{value}</dd></div>)}
      </dl>
      <div className="grid gap-6 xl:grid-cols-2">
        <section className="rounded-xl border border-slate-200 bg-white p-5" aria-labelledby="dashboard-states">
          <h2 id="dashboard-states" className="text-lg font-semibold">Duruma göre işler</h2>
          <p className="mt-1 text-sm text-slate-500">Tamamlananlar dahil · {total} iş</p>
          {total === 0 ? <p className="mt-5 text-sm text-slate-600">Görüntülenecek iş yok.</p> : <ul className="mt-5 space-y-4">{data.tasksByState.map(state =>
            <li key={state.stateId}><div className="mb-1 flex justify-between gap-3 text-sm"><span>{state.name}</span><span className="tabular-nums">{state.count}</span></div>
              <div role="meter" aria-label={state.name} aria-valuenow={state.count} aria-valuemin={0} aria-valuemax={total} aria-valuetext={`${state.count} / ${total} iş`} className="h-3 overflow-hidden rounded bg-slate-100"><div className="h-full rounded bg-indigo-600" style={{ width: `${100 * state.count / total}%` }} /></div></li>)}</ul>}
        </section>
        <section className="rounded-xl border border-slate-200 bg-white p-5" aria-labelledby="dashboard-mine">
          <h2 id="dashboard-mine" className="text-lg font-semibold">İşlerim</h2>
          <p className="mt-1 text-sm text-slate-500">Açık atamalarınız · Son tarih sırasıyla</p>
          {data.myTasks.length === 0 ? <p className="mt-5 text-sm text-slate-600">Bu sayfada size atanmış açık iş yok.</p> : <ul className="mt-4 divide-y divide-slate-100">{data.myTasks.map(task =>
            <li key={task.id} className="py-3"><Link className="block break-words text-sm font-medium text-indigo-700 hover:underline" to={workItemUrl(task.key)}>{task.key} · {task.title}</Link><p className="mt-1 text-xs text-slate-500">{task.state} · {task.dueDate ?? "Son tarih yok"}</p></li>)}</ul>}
          <div className="mt-4 flex flex-wrap items-center gap-3"><button className={buttonClass} disabled={page <= 1 || query.isFetching} onClick={() => setPage(page - 1)}>Önceki sayfa</button><span className="text-sm">Sayfa {page}</span><button className={buttonClass} disabled={page >= 100000 || page * data.myTasksPageSize >= data.myWorkItems || query.isFetching} onClick={() => setPage(page + 1)}>Sonraki sayfa</button></div>
        </section>
      </div>
      <section className="rounded-xl border border-slate-200 bg-white p-5" aria-labelledby="dashboard-activity">
        <h2 id="dashboard-activity" className="text-lg font-semibold">Son aktiviteler</h2><p className="mt-1 text-sm text-slate-500">Erişilebilir işlerdeki son 20 olay</p>
        {data.recentActivity.length === 0 ? <p className="mt-5 text-sm text-slate-600">Henüz aktivite yok.</p> : <ol className="mt-4 divide-y divide-slate-100">{data.recentActivity.map(activity =>
          <li key={activity.id} className="flex flex-wrap justify-between gap-2 py-3"><div className="min-w-0"><Link className="break-words text-sm font-medium text-indigo-700 hover:underline" to={workItemUrl(activity.workItemKey)}>{activity.workItemKey} · {activity.workItemTitle}</Link><p className="mt-1 break-all text-xs text-slate-500">{activity.type} · Aktör: {activity.actorId}</p></div><time className="text-xs text-slate-500" dateTime={activity.createdAt}>{new Date(activity.createdAt).toLocaleString()}</time></li>)}</ol>}
      </section>
    </> : null}
  </section>;
}

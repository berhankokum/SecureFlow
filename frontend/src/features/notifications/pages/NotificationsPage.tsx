import { LoadingState } from "@/components/ui/LoadingState";
import { ErrorState } from "@/components/ui/ErrorState";
import { useState } from "react";
import { ApiError } from "@/lib/api/apiClient";
import { useMarkAllNotificationsRead, useMarkNotificationRead, useNotificationPreference, useNotifications, useUpdateNotificationPreference } from "../hooks/useNotifications";

export function NotificationsPage() {
  const notifications = useNotifications();
  const preference = useNotificationPreference();
  const markRead = useMarkNotificationRead();
  const markAll = useMarkAllNotificationsRead();
  const updatePreference = useUpdateNotificationPreference();
  const [error, setError] = useState<string | null>(null);

  const run = async (action: () => Promise<unknown>) => {
    setError(null);
    try { await action(); }
    catch (cause) { setError(cause instanceof ApiError ? cause.message : "Bildirim ayarı güncellenemedi."); }
  };

  return <section className="max-w-4xl">
    <div className="flex flex-wrap items-center justify-between gap-3"><div><h1 className="text-2xl font-semibold text-slate-900">Bildirimler</h1><p className="mt-1 text-sm text-slate-600">En son 100 bildiriminiz gösterilir.</p></div>{(notifications.data?.unreadCount ?? 0) > 0 ? <button type="button" disabled={markAll.isPending} onClick={() => void run(() => markAll.mutateAsync())} className="rounded-md border border-slate-300 px-3 py-2 text-sm">Tümünü okundu işaretle</button> : null}</div>

    <section aria-labelledby="notification-settings-title" className="mt-6 rounded-lg border border-slate-200 bg-white p-5">
      <h2 id="notification-settings-title" className="font-semibold text-slate-900">Tercihler</h2>
      <label className="mt-3 flex items-center gap-3 text-sm text-slate-700"><input type="checkbox" checked disabled /> Uygulama içi bildirimler</label>
      <label className="mt-3 flex items-center gap-3 text-sm text-slate-700"><input type="checkbox" checked={preference.data?.emailEnabled ?? false} disabled={!preference.data || preference.isError || preference.isLoading || updatePreference.isPending} onChange={event => void run(() => updatePreference.mutateAsync(event.target.checked))} /> E-posta bildirimleri</label>
      {preference.isLoading ? <LoadingState label="Tercihler yükleniyor..." /> : null}
      {preference.isError ? <ErrorState message="Bildirim tercihleri yüklenemedi." onRetry={() => void preference.refetch()} retrying={preference.isFetching} /> : null}
      <p className="mt-2 text-xs text-slate-500">Tercih açıkken yeni uygulama içi bildirimleriniz e-posta olarak da gönderilir.</p>
    </section>

    {error ? <p role="alert" className="mt-4 rounded-md bg-red-50 p-3 text-sm text-red-700">{error}</p> : null}
    {notifications.isLoading ? <LoadingState label="Bildirimler yükleniyor..." />
      : notifications.isError ? <p role="alert" className="mt-6 text-sm text-red-700">Bildirimler yüklenemedi.</p>
        : notifications.data?.items.length ? <ul className="mt-6 space-y-3">{notifications.data.items.map(item => <li key={item.id} className={`rounded-lg border p-4 ${item.isRead ? "border-slate-200 bg-white" : "border-blue-200 bg-blue-50"}`}>
            <button type="button" disabled={item.isRead || markRead.isPending} onClick={() => void run(() => markRead.mutateAsync(item.id))} className="w-full text-left disabled:cursor-default">
              <span className="block font-medium text-slate-900">{item.title}</span><span className="mt-1 block text-sm text-slate-700">{item.message}</span><time className="mt-2 block text-xs text-slate-500" dateTime={item.createdAt}>{new Date(item.createdAt).toLocaleString("tr-TR")}</time>
            </button>
          </li>)}</ul>
          : <p className="mt-6 text-sm text-slate-500">Henüz bildirim yok.</p>}
  </section>;
}

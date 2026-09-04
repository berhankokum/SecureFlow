import { useState } from "react";
import { Link } from "react-router-dom";
import { useCurrentOrganization } from "@/features/organizations/hooks/useCurrentOrganization";
import { useMarkNotificationRead, useNotifications } from "../hooks/useNotifications";

export function NotificationBell() {
  const [open, setOpen] = useState(false);
  const organization = useCurrentOrganization();
  const notifications = useNotifications();
  const markRead = useMarkNotificationRead();
  const unreadCount = notifications.data?.unreadCount ?? 0;

  return <div className="relative">
    <button type="button" aria-label="Bildirimler" aria-expanded={open} onClick={() => setOpen(value => !value)} className="relative rounded-md border border-slate-300 px-3 py-1.5 text-base hover:bg-slate-50">
      <span aria-hidden="true">🔔</span>
      {unreadCount > 0 ? <span aria-label={`${unreadCount} okunmamış bildirim`} className="absolute -right-2 -top-2 min-w-5 rounded-full bg-red-700 px-1 text-center text-xs font-semibold text-white">{unreadCount > 99 ? "99+" : unreadCount}</span> : null}
    </button>
    {open ? <div className="absolute right-0 z-20 mt-2 w-80 rounded-lg border border-slate-200 bg-white p-3 shadow-lg">
      <div className="flex items-center justify-between"><h2 className="font-semibold text-slate-900">Bildirimler</h2><Link to={`/app/${organization.slug}/notifications`} onClick={() => setOpen(false)} className="text-xs font-medium text-slate-700 underline">Tümünü gör</Link></div>
      {notifications.isLoading ? <p className="mt-3 text-sm text-slate-500">Yükleniyor...</p>
        : notifications.isError ? <p role="alert" className="mt-3 text-sm text-red-700">Bildirimler yüklenemedi.</p>
          : notifications.data?.items.length ? <ul className="mt-3 divide-y divide-slate-100">{notifications.data.items.slice(0, 5).map(item => <li key={item.id} className="py-2">
              <button type="button" disabled={markRead.isPending} onClick={() => { if (!item.isRead) void markRead.mutateAsync(item.id); }} className="w-full text-left disabled:opacity-50">
                <span className={`block text-sm ${item.isRead ? "text-slate-600" : "font-semibold text-slate-900"}`}>{item.title}</span>
                <span className="mt-0.5 block text-xs text-slate-500">{item.message}</span>
              </button>
            </li>)}</ul>
            : <p className="mt-3 text-sm text-slate-500">Henüz bildirim yok.</p>}
    </div> : null}
  </div>;
}

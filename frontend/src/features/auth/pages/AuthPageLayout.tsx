import type { PropsWithChildren } from "react";

export function AuthPageLayout({ title, children }: PropsWithChildren<{ title: string }>) {
  return (
    <div className="flex min-h-screen items-center justify-center bg-slate-50 px-4">
      <div className="w-full max-w-sm space-y-6">
        <h1 className="text-center text-xl font-semibold text-slate-900">{title}</h1>
        <div className="rounded-lg border border-slate-200 bg-white p-6 shadow-sm">{children}</div>
      </div>
    </div>
  );
}

export function LoadingState({ label }: { label: string }) {
  return <div role="status" className="my-4 space-y-3 rounded-lg border border-slate-200 bg-white p-5">
    <span className="text-sm text-slate-600">{label}</span>
    <div aria-hidden="true" className="space-y-3 motion-safe:animate-pulse">
      <div className="h-4 w-2/3 rounded bg-slate-200" />
      <div className="h-4 w-full rounded bg-slate-200" />
      <div className="h-4 w-1/2 rounded bg-slate-200" />
    </div>
  </div>;
}

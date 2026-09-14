import { useState } from "react";
import type { ProjectDetails } from "@/features/projects/api/types";
import { useLabels } from "@/features/labels/hooks/useLabels";
import { useWorkflow } from "../hooks/useWorkItems";
import { useWorkItemListState, workItemFilterNames } from "../hooks/useWorkItemListState";

export function WorkItemFilters({ project }: { project: ProjectDetails }) {
  const { params, query, update } = useWorkItemListState();
  const labels = useLabels(project.id);
  const workflow = useWorkflow(project.id);
  const [error, setError] = useState<string | null>(null);
  const inputClass = "mt-1 block w-full rounded border border-slate-300 bg-white px-2 py-1 text-sm";
  const select = (name: string, title: string, options: [string, string][]) => {
    const current = params.get(name) ?? "";
    return <label>{title}<select name={name} defaultValue={current} className={inputClass}><option value="">Varsayılan / Tümü</option>{current && !options.some(([value]) => value === current) ? <option value={current}>{current}</option> : null}{options.map(([value, text]) => <option key={value} value={value}>{text}</option>)}</select></label>;
  };
  return <form key={query} aria-label="WorkItem filtreleri" className="mt-4 rounded-lg border border-slate-200 p-4" onSubmit={event => {
    event.preventDefault();
    const values = Object.fromEntries(new FormData(event.currentTarget)) as Record<string, string>;
    if ((values.createdFrom && values.createdTo && values.createdFrom > values.createdTo)
      || (values.dueFrom && values.dueTo && values.dueFrom > values.dueTo)) {
      setError("Başlangıç tarihi bitiş tarihinden sonra olamaz.");
      return;
    }
    setError(null);
    update(values);
  }}>
    <div className="grid gap-3 text-sm text-slate-700 sm:grid-cols-2 lg:grid-cols-4">
      <label>Arama<input name="search" type="search" maxLength={200} defaultValue={params.get("search") ?? ""} placeholder="Başlık veya açıklamadaki kelimeler" className={inputClass} /></label>
      {select("status", "Durum filtresi", (workflow.data?.states ?? []).map(state => [state.id, state.name]))}
      {select("priority", "Öncelik filtresi", ["LOW", "MEDIUM", "HIGH", "CRITICAL"].map(value => [value, value]))}
      {select("type", "Tür filtresi", ["TASK", "BUG", "STORY"].map(value => [value, value]))}
      {select("label", "Etiket filtresi", (labels.data ?? []).map(label => [label.id, label.name]))}
      {([['assignee', 'Atanan kullanıcı ID'], ['reporter', 'Oluşturan kullanıcı ID']] as const).map(([name, title]) => <label key={name}>{title}<input name={name} list={`work-item-members-${project.id}`} pattern="[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}" defaultValue={params.get(name) ?? ""} className={inputClass} /></label>)}
      <datalist id={`work-item-members-${project.id}`}>{project.members.map(member => <option key={member.userId} value={member.userId}>{member.email}</option>)}</datalist>
      {([['createdFrom', 'Oluşturulma başlangıcı (UTC)'], ['createdTo', 'Oluşturulma bitişi (UTC)'], ['dueFrom', 'Son tarih başlangıcı'], ['dueTo', 'Son tarih bitişi']] as const).map(([name, title]) => <label key={name}>{title}<input type="date" name={name} defaultValue={params.get(name) ?? ""} className={inputClass} /></label>)}
      {select("sort", "Sıralama alanı", [["createdAt", "Oluşturulma"], ["updatedAt", "Güncellenme"], ["dueDate", "Son tarih"], ["priority", "Öncelik"]])}
      {select("direction", "Sıralama yönü", [["asc", "Artan"], ["desc", "Azalan"]])}
      {select("pageSize", "Sayfa boyutu", [["10", "10"], ["25", "25"], ["50", "50"], ["100", "100"]])}
    </div>
    {labels.isError || workflow.isError ? <p role="alert">Filtre seçenekleri yüklenemedi. Sayfayı yeniden deneyin.</p> : null}
    {error ? <p role="alert" className="mt-2 text-red-700">{error}</p> : null}
    <div className="mt-3 flex gap-3"><button type="submit" className="rounded bg-slate-900 px-3 py-2 text-sm text-white">Filtreleri uygula</button><button type="button" onClick={() => { setError(null); update(Object.fromEntries(workItemFilterNames.map(name => [name, ""]))); }} className="rounded border px-3 py-2 text-sm">Filtreleri temizle</button></div>
  </form>;
}

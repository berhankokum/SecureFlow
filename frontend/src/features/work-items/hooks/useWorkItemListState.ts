import { useSearchParams } from "react-router-dom";

export const workItemFilterNames = ["search", "status", "priority", "type", "assignee", "reporter", "label", "createdFrom", "createdTo", "dueFrom", "dueTo", "sort", "direction", "page", "pageSize"] as const;

export function useWorkItemListState() {
  const [params, setParams] = useSearchParams();
  const query = new URLSearchParams();
  for (const name of workItemFilterNames) {
    const value = params.get(name);
    if (value) query.set(name, value);
  }
  const page = Number(query.get("page") ?? 1);
  const pageSize = Number(query.get("pageSize") ?? 25);
  const update = (values: Record<string, string>, resetPage = true) => {
    setParams(current => {
      const next = new URLSearchParams(current);
      for (const [name, value] of Object.entries(values)) {
        if (value) next.set(name, value); else next.delete(name);
      }
      if (resetPage) next.delete("page");
      return next;
    });
  };
  return { query: query.toString(), params, page, pageSize, update };
}

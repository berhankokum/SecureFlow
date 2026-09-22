import { useState } from "react";
import { LoadingState } from "@/components/ui/LoadingState";
import { Link } from "react-router-dom";
import { permissions } from "@/features/authorization/permissions";
import type { ProjectDetails } from "@/features/projects/api/types";
import { ApiError } from "@/lib/api/apiClient";
import type { WorkItem, WorkflowState, WorkflowTransition } from "../api/types";
import { useTransitionWorkItem, useWorkflow, useWorkItems } from "../hooks/useWorkItems";

function canUseTransition(
  item: WorkItem,
  transition: WorkflowTransition,
  projectArchived: boolean,
) {
  return !projectArchived
    && item.archivedAt === null
    && item.permissions.includes(permissions.workItemTransition)
    && (!transition.requiredPermission || item.permissions.includes(transition.requiredPermission));
}

export function ProjectKanbanBoard({ project }: { project: ProjectDetails }) {
  const workflowQuery = useWorkflow(project.id);
  const workItemsQuery = useWorkItems(project.id);
  const transitionMutation = useTransitionWorkItem(project.id);
  const [draggedWorkItemId, setDraggedWorkItemId] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  if (workflowQuery.isLoading || workItemsQuery.isLoading) {
    return <LoadingState label="Kanban board yükleniyor..." />;
  }
  if (workflowQuery.isError || workItemsQuery.isError || !workflowQuery.data) {
    return <p role="alert" className="mt-4 rounded-md bg-red-50 p-3 text-sm text-red-700">Kanban board yüklenemedi.</p>;
  }

  const workflow = workflowQuery.data;
  const items = workItemsQuery.data ?? [];
  const activeItems = items.filter(item => item.archivedAt === null);
  const stateById = new Map(workflow.states.map(state => [state.id, state]));

  const findTransition = (item: WorkItem, targetStateId: string) => workflow.transitions.find(
    transition => transition.fromStateId === item.workflowStateId
      && transition.toStateId === targetStateId,
  );

  const move = async (item: WorkItem, target: WorkflowState) => {
    const transition = findTransition(item, target.id);
    if (!transition || !canUseTransition(item, transition, project.archivedAt !== null)) return;
    setError(null);
    try {
      await transitionMutation.mutateAsync({ workItemId: item.id, toState: target, version: item.version });
    } catch (caught) {
      setError(caught instanceof ApiError ? caught.message : "Workflow transition tamamlanamadı.");
    }
  };

  return <div className="mt-4">
    <p className="text-sm text-slate-600">Kartları yalnızca izin verilen bir sonraki kolona sürükleyebilirsiniz. Klavyeyle kartın “Taşı...” listesini kullanabilirsiniz.</p>
    {error ? <p role="alert" className="mt-3 rounded-md bg-red-50 px-3 py-2 text-sm text-red-700">{error}</p> : null}
    <div className="mt-4 flex gap-4 overflow-x-auto pb-3" role="region" tabIndex={0} aria-label="Kanban board">
      {workflow.states.map(state => {
        const columnItems = activeItems.filter(item => item.workflowStateId === state.id);
        return <section
          key={state.id}
          aria-label={`${state.name} kolonu`}
          onDragOver={event => {
            const item = items.find(candidate => candidate.id === draggedWorkItemId);
            const transition = item ? findTransition(item, state.id) : undefined;
            if (item && transition && canUseTransition(item, transition, project.archivedAt !== null)) {
              event.preventDefault();
            }
          }}
          onDrop={event => {
            event.preventDefault();
            const item = items.find(candidate => candidate.id === draggedWorkItemId);
            setDraggedWorkItemId(null);
            if (item) void move(item, state);
          }}
          className="min-h-72 w-64 shrink-0 rounded-lg border border-slate-200 bg-slate-50 p-3"
        >
          <div className="flex items-center justify-between gap-2">
            <h3 className="text-sm font-semibold text-slate-800">{state.name.replaceAll("_", " ")}</h3>
            <span className="rounded-full bg-white px-2 py-0.5 text-xs text-slate-500">{columnItems.length}</span>
          </div>
          <div className="mt-3 space-y-3">
            {columnItems.map(item => {
              const allowed = workflow.transitions
                .filter(transition => transition.fromStateId === item.workflowStateId
                  && canUseTransition(item, transition, project.archivedAt !== null))
                .map(transition => stateById.get(transition.toStateId))
                .filter((target): target is WorkflowState => Boolean(target));
              return <article
                key={item.id}
                draggable={allowed.length > 0 && !transitionMutation.isPending}
                onDragStart={() => setDraggedWorkItemId(item.id)}
                onDragEnd={() => setDraggedWorkItemId(null)}
                className="rounded-md border border-slate-200 bg-white p-3 shadow-sm"
              >
                <Link to={`../../work-items/${item.key}`} relative="path" className="text-xs font-semibold text-slate-500 hover:underline">{item.key}</Link>
                <p className="mt-1 text-sm font-medium text-slate-900">{item.title}</p>
                <div className="mt-2 flex flex-wrap gap-1">{item.labels.map(label => <span key={label.id} className="rounded bg-blue-50 px-1.5 py-0.5 text-xs text-blue-800">{label.name}</span>)}</div>
                <div className="mt-3 flex items-center justify-between gap-2 text-xs text-slate-500"><span>{item.priority}</span><span className="truncate">{item.assigneeEmail ?? "Unassigned"}</span></div>
                {allowed.length > 0 ? <select
                  aria-label={`${item.key} hedef state`}
                  defaultValue=""
                  disabled={transitionMutation.isPending}
                  onChange={event => {
                    const target = stateById.get(event.target.value);
                    event.target.value = "";
                    if (target) void move(item, target);
                  }}
                  className="mt-3 w-full rounded border border-slate-300 px-2 py-1 text-xs"
                >
                  <option value="" disabled>Taşı...</option>
                  {allowed.map(target => <option key={target.id} value={target.id}>{target.name.replaceAll("_", " ")}</option>)}
                </select> : null}
              </article>;
            })}
            {columnItems.length === 0 ? <p className="rounded border border-dashed border-slate-300 p-3 text-center text-xs text-slate-500">Boş</p> : null}
          </div>
        </section>;
      })}
    </div>
    {items.some(item => item.archivedAt !== null) ? <p className="mt-2 text-xs text-slate-500">Archived WorkItem’lar board dışında tutulur ve liste görünümünden okunabilir.</p> : null}
  </div>;
}

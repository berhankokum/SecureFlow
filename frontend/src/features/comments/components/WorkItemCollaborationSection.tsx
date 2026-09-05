import { useId, useState } from "react";
import { zodResolver } from "@hookform/resolvers/zod";
import { useForm } from "react-hook-form";
import { permissions } from "@/features/authorization/permissions";
import type { WorkItem } from "@/features/work-items/api/types";
import { ApiError } from "@/lib/api/apiClient";
import type { ActivityEvent, Comment, CommentPayload } from "../api/types";
import { useActivity, useComments, useCreateComment, useDeleteComment, useUpdateComment } from "../hooks/useCollaboration";
import { commentSchema, type CommentFormValues } from "../schemas/commentSchema";

export interface MentionCandidate {
  userId: string;
  email: string;
}

function CommentEditor({ initialContent = "", initialMentions = [], mentionCandidates, submitLabel, onSubmit, onCancel }: {
  initialContent?: string;
  initialMentions?: MentionCandidate[];
  mentionCandidates: MentionCandidate[];
  submitLabel: string;
  onSubmit: (payload: CommentPayload) => Promise<void>;
  onCancel?: () => void;
}) {
  const id = useId();
  const mentionId = useId();
  const [requestError, setRequestError] = useState<string | null>(null);
  const [selectedUserIds, setSelectedUserIds] = useState(() => initialMentions.map(item => item.userId));
  const [candidateUserId, setCandidateUserId] = useState("");
  const { register, handleSubmit, reset, getValues, setValue, formState: { errors, isSubmitting } } = useForm<CommentFormValues>({
    resolver: zodResolver(commentSchema),
    defaultValues: { content: initialContent },
  });

  return <form noValidate className="space-y-3" onSubmit={handleSubmit(async values => {
    setRequestError(null);
    try {
      const mentionedUserIds = selectedUserIds.filter(userId => {
        const candidate = mentionCandidates.find(item => item.userId === userId);
        return candidate && values.content.toLocaleLowerCase("tr-TR").includes(`@${candidate.email.toLocaleLowerCase("tr-TR")}`);
      });
      await onSubmit({ content: values.content, mentionedUserIds });
      reset({ content: "" });
      setSelectedUserIds([]);
    } catch (error) {
      setRequestError(error instanceof ApiError ? error.message : "Yorum kaydedilemedi.");
    }
  })}>
    <label htmlFor={id} className="block text-sm font-medium text-slate-700">Yorum</label>
    <textarea id={id} rows={initialContent ? 4 : 3} maxLength={5_000} aria-invalid={!!errors.content} aria-describedby={errors.content ? `${id}-error` : undefined} className="w-full rounded-md border border-slate-300 px-3 py-2 text-sm" {...register("content")} />
    {errors.content ? <p id={`${id}-error`} role="alert" className="text-sm text-red-700">{errors.content.message}</p> : null}
    {requestError ? <p role="alert" className="text-sm text-red-700">{requestError}</p> : null}
    {mentionCandidates.length ? <div className="flex flex-wrap items-end gap-2">
      <label htmlFor={mentionId} className="flex-1 text-xs font-medium text-slate-600">Mention eklenecek kullanıcı
        <select id={mentionId} value={candidateUserId} onChange={event => setCandidateUserId(event.target.value)} className="mt-1 block w-full rounded-md border border-slate-300 px-2 py-2 text-sm">
          <option value="">Kullanıcı seçin</option>
          {mentionCandidates.filter(item => !selectedUserIds.includes(item.userId)).map(item => <option key={item.userId} value={item.userId}>{item.email}</option>)}
        </select>
      </label>
      <button type="button" disabled={!candidateUserId} onClick={() => {
        const candidate = mentionCandidates.find(item => item.userId === candidateUserId);
        if (!candidate) return;
        const current = getValues("content").trimEnd();
        setValue("content", `${current}${current ? " " : ""}@${candidate.email} `, { shouldDirty: true, shouldValidate: true });
        setSelectedUserIds(ids => [...ids, candidate.userId]);
        setCandidateUserId("");
      }} className="rounded-md border border-slate-300 px-3 py-2 text-sm disabled:opacity-50">Mention ekle</button>
    </div> : null}
    <div className="flex gap-3">
      <button type="submit" disabled={isSubmitting} className="rounded-md bg-slate-900 px-4 py-2 text-sm text-white disabled:opacity-50">{isSubmitting ? "Kaydediliyor..." : submitLabel}</button>
      {onCancel ? <button type="button" disabled={isSubmitting} onClick={onCancel} className="text-sm text-slate-600">Vazgeç</button> : null}
    </div>
  </form>;
}

function CommentRow({ comment, workItemId, mentionCandidates }: { comment: Comment; workItemId: string; mentionCandidates: MentionCandidate[] }) {
  const [editing, setEditing] = useState(false);
  const [deleteError, setDeleteError] = useState<string | null>(null);
  const update = useUpdateComment(workItemId);
  const remove = useDeleteComment(workItemId);

  return <li className="rounded-md border border-slate-200 p-4">
    <div className="flex flex-wrap items-center justify-between gap-2">
      <div><span className="text-sm font-medium text-slate-900">{comment.authorEmail}</span><span className="ml-2 text-xs text-slate-500">{new Date(comment.createdAt).toLocaleString("tr-TR")}{comment.updatedAt !== comment.createdAt && !comment.deletedAt ? " · düzenlendi" : ""}</span></div>
      {!comment.deletedAt ? <div className="flex gap-3">
        {comment.canUpdate ? <button type="button" onClick={() => setEditing(true)} className="text-xs font-medium text-slate-600">Düzenle</button> : null}
        {comment.canDelete ? <button type="button" disabled={remove.isPending} onClick={async () => {
          if (!window.confirm("Bu yorum silinsin mi?")) return;
          setDeleteError(null);
          try { await remove.mutateAsync(comment.id); }
          catch (error) { setDeleteError(error instanceof ApiError ? error.message : "Yorum silinemedi."); }
        }} className="text-xs font-medium text-red-700">Sil</button> : null}
      </div> : null}
    </div>
    {editing && comment.content ? <div className="mt-3"><CommentEditor initialContent={comment.content} initialMentions={comment.mentions} mentionCandidates={mentionCandidates} submitLabel="Değişiklikleri kaydet" onCancel={() => setEditing(false)} onSubmit={async payload => { await update.mutateAsync({ commentId: comment.id, ...payload }); setEditing(false); }} /></div>
      : comment.deletedAt ? <p className="mt-3 text-sm italic text-slate-500">Bu yorum silindi.</p>
        : <p className="mt-3 whitespace-pre-wrap text-sm text-slate-700">{comment.content}</p>}
    {deleteError ? <p role="alert" className="mt-2 text-sm text-red-700">{deleteError}</p> : null}
  </li>;
}

function activityText(activity: ActivityEvent): string {
  const metadata = activity.metadata;
  switch (activity.type) {
    case "WORK_ITEM_CREATED": return `${metadata.workItemKey ?? "WorkItem"} oluşturuldu`;
    case "WORK_ITEM_ASSIGNED": return metadata.previousAssigneeEmail
      ? `${metadata.previousAssigneeEmail} yerine ${metadata.assigneeEmail ?? "bir kullanıcı"} atandı`
      : `${metadata.assigneeEmail ?? "Bir kullanıcı"} atandı`;
    case "WORK_ITEM_UNASSIGNED": return `${metadata.previousAssigneeEmail ?? "Atanmış kullanıcı"} görevden çıkarıldı`;
    case "WORK_ITEM_TRANSITIONED": return `${metadata.fromState ?? "?"} → ${metadata.toState ?? "?"} geçişi yapıldı`;
    case "LABEL_ASSIGNED": return `${metadata.labelName ?? "Label"} etiketi eklendi`;
    case "LABEL_REMOVED": return `${metadata.labelName ?? "Label"} etiketi kaldırıldı`;
    case "COMMENT_CREATED": return "yorum ekledi";
    case "ATTACHMENT_UPLOADED": return `${metadata.fileName ?? "Bir dosya"} attachment'ını yükledi`;
    default: return String(activity.type).replaceAll("_", " ").toLocaleLowerCase("tr-TR");
  }
}

export function WorkItemCollaborationSection({ workItem, projectArchived, mentionCandidates = [] }: {
  workItem: WorkItem;
  projectArchived: boolean;
  mentionCandidates?: MentionCandidate[];
}) {
  const comments = useComments(workItem.id);
  const activity = useActivity(workItem.id);
  const create = useCreateComment(workItem.id);
  const canCreate = !projectArchived
    && workItem.archivedAt === null
    && workItem.permissions.includes(permissions.commentCreate);

  return <div className="mt-6 grid gap-6 lg:grid-cols-2">
    <section aria-labelledby="comments-title" className="rounded-lg border border-slate-200 bg-white p-5">
      <h2 id="comments-title" className="text-lg font-semibold text-slate-900">Yorumlar</h2>
      {canCreate ? <div className="mt-4"><CommentEditor mentionCandidates={mentionCandidates} submitLabel="Yorum ekle" onSubmit={async payload => { await create.mutateAsync(payload); }} /></div> : null}
      {comments.isLoading ? <p className="mt-4 text-sm text-slate-500">Yorumlar yükleniyor...</p>
        : comments.isError ? <p role="alert" className="mt-4 text-sm text-red-700">Yorumlar yüklenemedi.</p>
          : comments.data?.length ? <ul className="mt-5 space-y-3">{comments.data.map(comment => <CommentRow key={comment.id} comment={comment} workItemId={workItem.id} mentionCandidates={mentionCandidates} />)}</ul>
            : <p className="mt-4 text-sm text-slate-500">Henüz yorum yok.</p>}
    </section>

    <section aria-labelledby="activity-title" className="rounded-lg border border-slate-200 bg-white p-5">
      <h2 id="activity-title" className="text-lg font-semibold text-slate-900">Aktivite</h2>
      {activity.isLoading ? <p className="mt-4 text-sm text-slate-500">Aktivite yükleniyor...</p>
        : activity.isError ? <p role="alert" className="mt-4 text-sm text-red-700">Aktivite yüklenemedi.</p>
          : activity.data?.length ? <ol className="mt-4 space-y-4 border-l border-slate-200 pl-4">{activity.data.map(event => <li key={event.id}>
              <p className="text-sm text-slate-700"><span className="font-medium text-slate-900">{event.actorEmail}</span> {activityText(event)}</p>
              <time className="text-xs text-slate-500" dateTime={event.createdAt}>{new Date(event.createdAt).toLocaleString("tr-TR")}</time>
            </li>)}</ol>
            : <p className="mt-4 text-sm text-slate-500">Henüz aktivite yok.</p>}
    </section>
  </div>;
}

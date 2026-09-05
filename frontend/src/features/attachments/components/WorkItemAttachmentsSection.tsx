import { useRef, useState } from "react";
import { permissions } from "@/features/authorization/permissions";
import type { WorkItem } from "@/features/work-items/api/types";
import { ApiError } from "@/lib/api/apiClient";
import { attachmentApi } from "../api/attachmentApi";
import type { Attachment } from "../api/types";
import { useAttachments, useDeleteAttachment, useUploadAttachment } from "../hooks/useAttachments";

const MAXIMUM_FILE_SIZE = 10 * 1024 * 1024;
const ALLOWED_EXTENSIONS = [".pdf", ".png", ".jpg", ".jpeg"];

function formatSize(size: number): string {
  if (size >= 1024 * 1024) return `${(size / (1024 * 1024)).toFixed(1)} MiB`;
  if (size >= 1024) return `${(size / 1024).toFixed(1)} KiB`;
  return `${size} B`;
}

function AttachmentRow({ attachment, workItemId }: { attachment: Attachment; workItemId: string }) {
  const remove = useDeleteAttachment(workItemId);
  const [error, setError] = useState<string | null>(null);

  const download = async () => {
    setError(null);
    try { await attachmentApi.download(attachment); }
    catch (downloadError) {
      setError(downloadError instanceof ApiError ? downloadError.message : "Dosya indirilemedi.");
    }
  };

  return <li className="flex flex-wrap items-center justify-between gap-3 rounded-md border border-slate-200 p-3">
    <div className="min-w-0">
      <p className="truncate text-sm font-medium text-slate-900">{attachment.originalFileName}</p>
      <p className="mt-1 text-xs text-slate-500">{formatSize(attachment.size)} · {attachment.uploadedByEmail} · {new Date(attachment.uploadedAt).toLocaleString("tr-TR")}</p>
      {error ? <p role="alert" className="mt-1 text-xs text-red-700">{error}</p> : null}
    </div>
    <div className="flex gap-3">
      <button type="button" onClick={() => void download()} className="text-xs font-medium text-slate-700">İndir</button>
      {attachment.canDelete ? <button type="button" disabled={remove.isPending} onClick={async () => {
        if (!window.confirm("Bu attachment silinsin mi?")) return;
        setError(null);
        try { await remove.mutateAsync(attachment.id); }
        catch (deleteError) { setError(deleteError instanceof ApiError ? deleteError.message : "Dosya silinemedi."); }
      }} className="text-xs font-medium text-red-700">Sil</button> : null}
    </div>
  </li>;
}

export function WorkItemAttachmentsSection({ workItem, projectArchived }: {
  workItem: WorkItem;
  projectArchived: boolean;
}) {
  const inputRef = useRef<HTMLInputElement>(null);
  const attachments = useAttachments(workItem.id);
  const upload = useUploadAttachment(workItem.id);
  const [progress, setProgress] = useState<number | null>(null);
  const [error, setError] = useState<string | null>(null);
  const canUpload = !projectArchived
    && workItem.archivedAt === null
    && workItem.permissions.includes(permissions.attachmentUpload);

  const uploadFile = async (file?: File) => {
    if (!file) return;
    setError(null);
    const extension = file.name.slice(file.name.lastIndexOf(".")).toLocaleLowerCase("en-US");
    if (!ALLOWED_EXTENSIONS.includes(extension)) {
      setError("Yalnız PDF, PNG ve JPEG dosyaları yüklenebilir.");
      return;
    }
    if (file.size === 0 || file.size > MAXIMUM_FILE_SIZE) {
      setError("Dosya boş olamaz ve 10 MiB sınırını aşamaz.");
      return;
    }

    setProgress(0);
    try { await upload.mutateAsync({ file, onProgress: setProgress }); }
    catch (uploadError) { setError(uploadError instanceof ApiError ? uploadError.message : "Dosya yüklenemedi."); }
    finally {
      setProgress(null);
      if (inputRef.current) inputRef.current.value = "";
    }
  };

  return <section aria-labelledby="attachments-title" className="mt-6 rounded-lg border border-slate-200 bg-white p-5">
    <div className="flex flex-wrap items-center justify-between gap-3">
      <div><h2 id="attachments-title" className="text-lg font-semibold text-slate-900">Attachments</h2><p className="mt-1 text-xs text-slate-500">PDF, PNG veya JPEG · en fazla 10 MiB</p></div>
      {canUpload ? <><input ref={inputRef} className="sr-only" type="file" aria-label="Dosya seç" accept=".pdf,.png,.jpg,.jpeg,application/pdf,image/png,image/jpeg" onChange={event => void uploadFile(event.target.files?.[0])} /><button type="button" disabled={upload.isPending} onClick={() => inputRef.current?.click()} className="rounded-md bg-slate-900 px-4 py-2 text-sm text-white disabled:opacity-50">Dosya seç</button></> : null}
    </div>
    {canUpload ? <div onDragOver={event => event.preventDefault()} onDrop={event => { event.preventDefault(); void uploadFile(event.dataTransfer.files[0]); }} className="mt-4 rounded-md border border-dashed border-slate-300 px-4 py-5 text-center text-sm text-slate-500">Dosyayı buraya sürükleyip bırakabilirsiniz.</div> : null}
    {progress !== null ? <div className="mt-4"><div className="flex justify-between text-xs text-slate-600"><span>Yükleniyor</span><span>{progress}%</span></div><progress aria-label="Yükleme ilerlemesi" className="mt-1 w-full" max={100} value={progress} /></div> : null}
    {error ? <p role="alert" className="mt-3 rounded-md bg-red-50 px-3 py-2 text-sm text-red-700">{error}</p> : null}
    {attachments.isLoading ? <p className="mt-4 text-sm text-slate-500">Attachment’lar yükleniyor...</p>
      : attachments.isError ? <p role="alert" className="mt-4 text-sm text-red-700">Attachment listesi yüklenemedi.</p>
        : attachments.data?.length ? <ul className="mt-4 space-y-3">{attachments.data.map(attachment => <AttachmentRow key={attachment.id} attachment={attachment} workItemId={workItem.id} />)}</ul>
          : <p className="mt-4 text-sm text-slate-500">Henüz attachment yok.</p>}
  </section>;
}

import { useEffect, useState } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { ApiError } from "@/lib/api/apiClient";
import { useCreateInvitation } from "../hooks/useCreateInvitation";
import { invitationSchema, type InvitationFormValues } from "../schemas/invitationSchemas";

interface InviteMemberDialogProps {
  organizationId: string;
  canInviteOwner: boolean;
  onDismiss: () => void;
}

export function InviteMemberDialog({
  organizationId,
  canInviteOwner,
  onDismiss,
}: InviteMemberDialogProps) {
  const [formError, setFormError] = useState<string | null>(null);
  const createInvitationMutation = useCreateInvitation(organizationId);
  const {
    register,
    handleSubmit,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<InvitationFormValues>({
    resolver: zodResolver(invitationSchema),
    defaultValues: { email: "", role: "MEMBER" },
  });

  useEffect(() => {
    const closeOnEscape = (event: KeyboardEvent) => {
      if (event.key === "Escape") {
        onDismiss();
      }
    };
    document.addEventListener("keydown", closeOnEscape);
    return () => document.removeEventListener("keydown", closeOnEscape);
  }, [onDismiss]);

  const onSubmit = handleSubmit(async (values) => {
    setFormError(null);
    try {
      await createInvitationMutation.mutateAsync(values);
      onDismiss();
    } catch (error) {
      if (error instanceof ApiError && error.errors) {
        for (const [fieldName, messages] of Object.entries(error.errors)) {
          setError(fieldName.toLowerCase() as keyof InvitationFormValues, {
            message: messages[0],
          });
        }
        return;
      }

      setFormError(error instanceof ApiError ? error.message : "Invitation gönderilemedi.");
    }
  });

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-slate-950/40 p-4"
      role="presentation"
      onMouseDown={(event) => {
        if (event.currentTarget === event.target) {
          onDismiss();
        }
      }}
    >
      <div
        role="dialog"
        aria-modal="true"
        aria-labelledby="invite-member-title"
        className="w-full max-w-md rounded-xl bg-white p-6 shadow-xl"
      >
        <div className="flex items-center justify-between gap-4">
          <h2 id="invite-member-title" className="text-lg font-semibold text-slate-900">
            Üye davet et
          </h2>
          <button
            type="button"
            aria-label="Davet penceresini kapat"
            onClick={onDismiss}
            className="rounded px-2 py-1 text-slate-500 hover:bg-slate-100"
          >
            ×
          </button>
        </div>

        <form onSubmit={onSubmit} className="mt-5 space-y-4" noValidate>
          {formError ? (
            <p className="rounded-md bg-red-50 px-3 py-2 text-sm text-red-700">{formError}</p>
          ) : null}

          <div>
            <label htmlFor="invitation-email" className="block text-sm font-medium text-slate-700">
              E-posta
            </label>
            <input
              id="invitation-email"
              type="email"
              autoComplete="email"
              autoFocus
              className="mt-1 w-full rounded-md border border-slate-300 px-3 py-2 text-sm"
              {...register("email")}
            />
            {errors.email ? <p className="mt-1 text-sm text-red-600">{errors.email.message}</p> : null}
          </div>

          <div>
            <label htmlFor="invitation-role" className="block text-sm font-medium text-slate-700">
              Organization rolü
            </label>
            <select
              id="invitation-role"
              className="mt-1 w-full rounded-md border border-slate-300 px-3 py-2 text-sm"
              {...register("role")}
            >
              <option value="MEMBER">MEMBER</option>
              <option value="ADMIN">ADMIN</option>
              {canInviteOwner ? <option value="OWNER">OWNER</option> : null}
            </select>
          </div>

          <div className="flex justify-end gap-3 pt-2">
            <button
              type="button"
              onClick={onDismiss}
              className="rounded-md border border-slate-300 px-4 py-2 text-sm"
            >
              Vazgeç
            </button>
            <button
              type="submit"
              disabled={isSubmitting}
              className="rounded-md bg-slate-900 px-4 py-2 text-sm font-medium text-white disabled:opacity-50"
            >
              {isSubmitting ? "Gönderiliyor..." : "Davet gönder"}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}

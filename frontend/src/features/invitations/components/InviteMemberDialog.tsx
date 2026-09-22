import { useState } from "react";
import { Dialog } from "@/components/ui/Dialog";
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
    <Dialog labelledBy="invite-member-title" onDismiss={onDismiss}>
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
            <p role="alert" className="rounded-md bg-red-50 px-3 py-2 text-sm text-red-700">{formError}</p>
          ) : null}

          <div>
            <label htmlFor="invitation-email" className="block text-sm font-medium text-slate-700">
              E-posta
            </label>
            <input
              id="invitation-email"
              type="email"
              autoComplete="email"
              data-dialog-autofocus
              aria-invalid={Boolean(errors.email)}
              aria-describedby={errors.email ? "invitation-email-error" : undefined}
              className="mt-1 w-full rounded-md border border-slate-300 px-3 py-2 text-sm"
              {...register("email")}
            />
            {errors.email ? <p id="invitation-email-error" role="alert" className="mt-1 text-sm text-red-600">{errors.email.message}</p> : null}
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
    </Dialog>
  );
}

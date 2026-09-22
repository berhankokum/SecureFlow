import { useId, useState } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { useNavigate } from "react-router-dom";
import { ApiError } from "@/lib/api/apiClient";
import { useCreateOrganization } from "../hooks/useCreateOrganization";
import {
  createOrganizationSchema,
  type CreateOrganizationFormValues,
} from "../schemas/organizationSchemas";

export function CreateOrganizationForm() {
  const errorId = useId();
  const navigate = useNavigate();
  const createOrganizationMutation = useCreateOrganization();
  const [formError, setFormError] = useState<string | null>(null);

  const {
    register,
    handleSubmit,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<CreateOrganizationFormValues>({
    resolver: zodResolver(createOrganizationSchema),
    defaultValues: { name: "", logoUrl: "" },
  });

  const onSubmit = handleSubmit(async (values) => {
    setFormError(null);

    try {
      const organization = await createOrganizationMutation.mutateAsync({
        name: values.name,
        logoUrl: values.logoUrl || undefined,
      });
      navigate(`/app/${organization.slug}/dashboard`, { replace: true });
    } catch (error) {
      if (error instanceof ApiError && error.errors) {
        for (const [fieldName, messages] of Object.entries(error.errors)) {
          setError(fieldName.toLowerCase() as keyof CreateOrganizationFormValues, {
            message: messages[0],
          });
        }
        return;
      }

      setFormError(
        error instanceof ApiError
          ? error.message
          : "Organization oluşturulurken bir hata oluştu.",
      );
    }
  });

  return (
    <form onSubmit={onSubmit} className="space-y-5" noValidate>
      {formError ? (
        <p role="alert" className="rounded-md bg-red-50 px-3 py-2 text-sm text-red-700">{formError}</p>
      ) : null}

      <div>
        <label htmlFor="organization-name" className="block text-sm font-medium text-slate-700">
          Organization adı
        </label>
        <input
          id="organization-name"
          autoComplete="organization"
          className="mt-1 w-full rounded-md border border-slate-300 px-3 py-2 text-sm"
          aria-invalid={Boolean(errors.name)} aria-describedby={errors.name ? `${errorId}-name` : undefined} {...register("name")}
        />
        {errors.name ? <p id={`${errorId}-name`} role="alert" className="mt-1 text-sm text-red-600">{errors.name.message}</p> : null}
      </div>

      <div>
        <label htmlFor="organization-logo" className="block text-sm font-medium text-slate-700">
          Logo adresi <span className="font-normal text-slate-500">(isteğe bağlı)</span>
        </label>
        <input
          id="organization-logo"
          type="url"
          placeholder="https://example.com/logo.png"
          className="mt-1 w-full rounded-md border border-slate-300 px-3 py-2 text-sm"
          aria-invalid={Boolean(errors.logoUrl)} aria-describedby={errors.logoUrl ? `${errorId}-logoUrl` : undefined} {...register("logoUrl")}
        />
        {errors.logoUrl ? (
          <p id={`${errorId}-logoUrl`} role="alert" className="mt-1 text-sm text-red-600">{errors.logoUrl.message}</p>
        ) : null}
      </div>

      <button
        type="submit"
        disabled={isSubmitting}
        className="w-full rounded-md bg-slate-900 px-4 py-2 text-sm font-medium text-white disabled:opacity-50"
      >
        {isSubmitting ? "Organization oluşturuluyor..." : "Organization oluştur"}
      </button>
    </form>
  );
}

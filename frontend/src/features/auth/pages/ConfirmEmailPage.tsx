import { useEffect, useRef, useState } from "react";
import { Link, useSearchParams } from "react-router-dom";
import { useConfirmEmail } from "../hooks/useConfirmEmail";
import { ApiError } from "@/lib/api/apiClient";
import { AuthPageLayout } from "./AuthPageLayout";

type Status = "confirming" | "success" | "error";

export function ConfirmEmailPage() {
  const [searchParams] = useSearchParams();
  const userId = searchParams.get("userId");
  const code = searchParams.get("code");

  const confirmEmailMutation = useConfirmEmail();
  const [status, setStatus] = useState<Status>("confirming");
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const requested = useRef(false);

  useEffect(() => {
    if (requested.current) {
      return;
    }
    requested.current = true;

    if (!userId || !code) {
      setStatus("error");
      setErrorMessage("Bağlantı geçersiz.");
      return;
    }

    confirmEmailMutation
      .mutateAsync({ userId, code })
      .then(() => setStatus("success"))
      .catch((error) => {
        setStatus("error");
        setErrorMessage(
          error instanceof ApiError ? error.message : "E-posta doğrulanamadı veya bağlantının süresi dolmuş.",
        );
      });
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [userId, code]);

  return (
    <AuthPageLayout title="E-posta doğrulama">
      {status === "confirming" ? <p className="text-sm text-slate-600">Doğrulanıyor...</p> : null}
      {status === "success" ? (
        <div className="space-y-3 text-sm text-slate-700">
          <p>E-postanız doğrulandı.</p>
          <Link to="/login" className="underline">
            Girişe dön
          </Link>
        </div>
      ) : null}
      {status === "error" ? <p role="alert" className="text-sm text-red-600">{errorMessage}</p> : null}
    </AuthPageLayout>
  );
}

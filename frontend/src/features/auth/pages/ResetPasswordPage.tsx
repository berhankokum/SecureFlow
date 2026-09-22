import { useSearchParams } from "react-router-dom";
import { ResetPasswordForm } from "../components/ResetPasswordForm";
import { AuthPageLayout } from "./AuthPageLayout";

export function ResetPasswordPage() {
  const [searchParams] = useSearchParams();
  const email = searchParams.get("email");
  const code = searchParams.get("code");

  return (
    <AuthPageLayout title="Şifreni sıfırla">
      {email && code ? (
        <ResetPasswordForm email={email} code={code} />
      ) : (
        <p role="alert" className="text-sm text-red-600">
          Bağlantı geçersiz. Lütfen şifre sıfırlama e-postasındaki bağlantıyı kullanın.
        </p>
      )}
    </AuthPageLayout>
  );
}

import { ForgotPasswordForm } from "../components/ForgotPasswordForm";
import { AuthPageLayout } from "./AuthPageLayout";

export function ForgotPasswordPage() {
  return (
    <AuthPageLayout title="Şifremi unuttum">
      <ForgotPasswordForm />
    </AuthPageLayout>
  );
}

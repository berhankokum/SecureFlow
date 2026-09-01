import { LoginForm } from "../components/LoginForm";
import { AuthPageLayout } from "./AuthPageLayout";

export function LoginPage() {
  return (
    <AuthPageLayout title="SecureFlow'a giriş yap">
      <LoginForm />
    </AuthPageLayout>
  );
}

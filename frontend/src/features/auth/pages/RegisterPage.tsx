import { RegisterForm } from "../components/RegisterForm";
import { AuthPageLayout } from "./AuthPageLayout";

export function RegisterPage() {
  return (
    <AuthPageLayout title="SecureFlow'a katıl">
      <RegisterForm />
    </AuthPageLayout>
  );
}

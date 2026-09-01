import { useState, type ReactNode } from "react";
import { Link, useLocation, useNavigate, useParams } from "react-router-dom";
import { useCurrentUser } from "@/features/auth/hooks/useCurrentUser";
import { ApiError } from "@/lib/api/apiClient";
import { useAcceptInvitation } from "../hooks/useAcceptInvitation";
import { useInvitation } from "../hooks/useInvitation";

const statusMessages = {
  ACCEPTED: "Bu invitation daha önce kullanılmış.",
  REVOKED: "Bu invitation revoke edilmiş.",
  EXPIRED: "Bu invitation'ın süresi dolmuş.",
} as const;

export function InvitationAcceptancePage() {
  const { token } = useParams();
  const location = useLocation();
  const navigate = useNavigate();
  const invitationQuery = useInvitation(token);
  const { data: currentUser, isLoading: isUserLoading } = useCurrentUser();
  const acceptInvitationMutation = useAcceptInvitation(token ?? "");
  const [actionError, setActionError] = useState<string | null>(null);

  if (!token) {
    return <InvitationLayout><p className="text-sm text-red-700">Invitation bağlantısı geçersiz.</p></InvitationLayout>;
  }

  if (invitationQuery.isLoading) {
    return <InvitationLayout><p className="text-sm text-slate-600">Invitation doğrulanıyor...</p></InvitationLayout>;
  }

  if (invitationQuery.isError || !invitationQuery.data) {
    return <InvitationLayout><p className="text-sm text-red-700">Invitation bulunamadı veya bağlantı geçersiz.</p></InvitationLayout>;
  }

  const invitation = invitationQuery.data;
  const inactiveMessage = invitation.status === "PENDING"
    ? null
    : statusMessages[invitation.status];
  const emailMatches = currentUser?.email.toLowerCase() === invitation.email.toLowerCase();

  const accept = async () => {
    setActionError(null);
    try {
      const accepted = await acceptInvitationMutation.mutateAsync();
      navigate(`/app/${accepted.organizationSlug}/members`, { replace: true });
    } catch (error) {
      setActionError(error instanceof ApiError ? error.message : "Invitation kabul edilemedi.");
    }
  };

  return (
    <InvitationLayout>
      <h1 className="text-xl font-semibold text-slate-900">{invitation.organizationName}</h1>
      <p className="mt-3 text-sm text-slate-600">
        <strong>{invitation.email}</strong> adresi {invitation.role} rolüyle davet edildi.
      </p>

      {inactiveMessage ? (
        <p className="mt-5 rounded-md bg-amber-50 px-3 py-2 text-sm text-amber-800">{inactiveMessage}</p>
      ) : null}

      {actionError ? (
        <p className="mt-5 rounded-md bg-red-50 px-3 py-2 text-sm text-red-700">{actionError}</p>
      ) : null}

      {!inactiveMessage && !isUserLoading && !currentUser ? (
        <div className="mt-6 space-y-3 text-sm">
          <p>Invitation'ı kabul etmek için davet edilen hesapla giriş yapın.</p>
          <div className="flex gap-4">
            <Link
              to="/login"
              state={{ from: location }}
              className="rounded-md bg-slate-900 px-4 py-2 font-medium text-white"
            >
              Giriş yap
            </Link>
            <Link to="/register" className="rounded-md border border-slate-300 px-4 py-2">
              Hesap oluştur
            </Link>
          </div>
        </div>
      ) : null}

      {!inactiveMessage && currentUser && !emailMatches ? (
        <p className="mt-5 rounded-md bg-red-50 px-3 py-2 text-sm text-red-700">
          Bu invitation {invitation.email} hesabına ait. Şu anda {currentUser.email} hesabıyla giriş yaptınız.
        </p>
      ) : null}

      {!inactiveMessage && currentUser && emailMatches ? (
        <button
          type="button"
          onClick={() => void accept()}
          disabled={acceptInvitationMutation.isPending}
          className="mt-6 w-full rounded-md bg-slate-900 px-4 py-2 text-sm font-medium text-white disabled:opacity-50"
        >
          {acceptInvitationMutation.isPending ? "Kabul ediliyor..." : "Invitation'ı kabul et"}
        </button>
      ) : null}
    </InvitationLayout>
  );
}

function InvitationLayout({ children }: { children: ReactNode }) {
  return (
    <main className="flex min-h-screen items-center justify-center bg-slate-50 p-6">
      <section className="w-full max-w-lg rounded-xl border border-slate-200 bg-white p-7 shadow-sm">
        <div className="mb-6 text-lg font-semibold text-slate-900">SecureFlow</div>
        {children}
      </section>
    </main>
  );
}

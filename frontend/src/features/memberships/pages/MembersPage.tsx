import { useState } from "react";
import { useNavigate } from "react-router-dom";
import { useCurrentUser } from "@/features/auth/hooks/useCurrentUser";
import { InviteMemberDialog } from "@/features/invitations/components/InviteMemberDialog";
import { useInvitations } from "@/features/invitations/hooks/useInvitations";
import { useRevokeInvitation } from "@/features/invitations/hooks/useRevokeInvitation";
import { useCurrentOrganization } from "@/features/organizations/hooks/useCurrentOrganization";
import { ApiError } from "@/lib/api/apiClient";
import type { OrganizationMember, OrganizationRole } from "../api/types";
import { useChangeMemberRole } from "../hooks/useChangeMemberRole";
import { useMembers } from "../hooks/useMembers";
import { useRemoveMember } from "../hooks/useRemoveMember";

const formatDate = (value: string) => new Intl.DateTimeFormat("tr-TR", {
  dateStyle: "medium",
  timeStyle: "short",
}).format(new Date(value));

export function MembersPage() {
  const organization = useCurrentOrganization();
  const { data: currentUser } = useCurrentUser();
  const canManageInvitations = organization.role === "OWNER" || organization.role === "ADMIN";
  const membersQuery = useMembers(organization.id);
  const invitationsQuery = useInvitations(organization.id, canManageInvitations);
  const changeRoleMutation = useChangeMemberRole(organization.id);
  const removeMemberMutation = useRemoveMember(organization.id);
  const revokeInvitationMutation = useRevokeInvitation(organization.id);
  const navigate = useNavigate();
  const [showInviteDialog, setShowInviteDialog] = useState(false);
  const [actionError, setActionError] = useState<string | null>(null);

  const changeRole = async (member: OrganizationMember, role: OrganizationRole) => {
    setActionError(null);
    try {
      await changeRoleMutation.mutateAsync({ userId: member.userId, role });
    } catch (error) {
      setActionError(error instanceof ApiError ? error.message : "Rol değiştirilemedi.");
    }
  };

  const removeMember = async (member: OrganizationMember) => {
    const isSelf = member.userId === currentUser?.id;
    const message = isSelf
      ? "Bu organization'dan ayrılmak istediğinize emin misiniz?"
      : `${member.email} kullanıcısını organization'dan çıkarmak istediğinize emin misiniz?`;

    if (!window.confirm(message)) {
      return;
    }

    setActionError(null);
    try {
      await removeMemberMutation.mutateAsync(member.userId);
      if (isSelf) {
        navigate("/", { replace: true });
      }
    } catch (error) {
      setActionError(error instanceof ApiError ? error.message : "Üye çıkarılamadı.");
    }
  };

  const canRemove = (member: OrganizationMember) => {
    if (member.userId === currentUser?.id) {
      return true;
    }
    if (organization.role === "OWNER") {
      return true;
    }
    return organization.role === "ADMIN" && member.role === "MEMBER";
  };

  const revokeInvitation = async (invitationId: string) => {
    setActionError(null);
    try {
      await revokeInvitationMutation.mutateAsync(invitationId);
    } catch (error) {
      setActionError(error instanceof ApiError ? error.message : "Invitation revoke edilemedi.");
    }
  };

  return (
    <section>
      <div className="flex flex-wrap items-center justify-between gap-4">
        <div>
          <h1 className="text-2xl font-semibold text-slate-900">Üyeler</h1>
          <p className="mt-1 text-sm text-slate-600">
            {organization.name} üyelerini ve bekleyen davetleri yönetin.
          </p>
        </div>
        {canManageInvitations ? (
          <button
            type="button"
            onClick={() => setShowInviteDialog(true)}
            className="rounded-md bg-slate-900 px-4 py-2 text-sm font-medium text-white"
          >
            Üye davet et
          </button>
        ) : null}
      </div>

      {actionError ? (
        <p className="mt-5 rounded-md bg-red-50 px-3 py-2 text-sm text-red-700">{actionError}</p>
      ) : null}

      <div className="mt-6 overflow-x-auto rounded-lg border border-slate-200 bg-white">
        {membersQuery.isLoading ? <p className="p-5 text-sm text-slate-600">Üyeler yükleniyor...</p> : null}
        {membersQuery.isError ? <p className="p-5 text-sm text-red-700">Üyeler yüklenemedi.</p> : null}
        {membersQuery.data ? (
          <table className="min-w-full divide-y divide-slate-200 text-left text-sm">
            <thead className="bg-slate-50 text-slate-600">
              <tr>
                <th className="px-5 py-3 font-medium">E-posta</th>
                <th className="px-5 py-3 font-medium">Rol</th>
                <th className="px-5 py-3 font-medium">Katılım</th>
                <th className="px-5 py-3 text-right font-medium">İşlem</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {membersQuery.data.map((member) => (
                <tr key={member.id}>
                  <td className="px-5 py-4 text-slate-900">
                    {member.email}
                    {member.userId === currentUser?.id ? (
                      <span className="ml-2 text-xs text-slate-500">(siz)</span>
                    ) : null}
                  </td>
                  <td className="px-5 py-4">
                    {organization.role === "OWNER" ? (
                      <select
                        aria-label={`${member.email} rolü`}
                        value={member.role}
                        disabled={changeRoleMutation.isPending}
                        onChange={(event) => void changeRole(member, event.target.value as OrganizationRole)}
                        className="rounded-md border border-slate-300 px-2 py-1 text-sm"
                      >
                        <option value="OWNER">OWNER</option>
                        <option value="ADMIN">ADMIN</option>
                        <option value="MEMBER">MEMBER</option>
                      </select>
                    ) : (
                      <span>{member.role}</span>
                    )}
                  </td>
                  <td className="px-5 py-4 text-slate-600">{formatDate(member.joinedAt)}</td>
                  <td className="px-5 py-4 text-right">
                    {canRemove(member) ? (
                      <button
                        type="button"
                        disabled={removeMemberMutation.isPending}
                        onClick={() => void removeMember(member)}
                        className="text-sm font-medium text-red-700 hover:underline disabled:opacity-50"
                      >
                        {member.userId === currentUser?.id ? "Ayrıl" : "Çıkar"}
                      </button>
                    ) : null}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        ) : null}
      </div>

      {canManageInvitations ? (
        <section className="mt-8">
          <h2 className="text-lg font-semibold text-slate-900">Bekleyen davetler</h2>
          <div className="mt-3 rounded-lg border border-slate-200 bg-white">
            {invitationsQuery.isLoading ? <p className="p-5 text-sm text-slate-600">Davetler yükleniyor...</p> : null}
            {invitationsQuery.isError ? <p className="p-5 text-sm text-red-700">Davetler yüklenemedi.</p> : null}
            {invitationsQuery.data?.length === 0 ? (
              <p className="p-5 text-sm text-slate-500">Bekleyen davet yok.</p>
            ) : null}
            {invitationsQuery.data?.map((invitation) => (
              <div key={invitation.id} className="flex flex-wrap items-center justify-between gap-3 border-b border-slate-100 px-5 py-4 last:border-b-0">
                <div>
                  <p className="text-sm font-medium text-slate-900">{invitation.email}</p>
                  <p className="mt-1 text-xs text-slate-500">
                    {invitation.role} · {formatDate(invitation.expiresAt)} tarihinde sona erer
                  </p>
                </div>
                <button
                  type="button"
                  disabled={revokeInvitationMutation.isPending}
                  onClick={() => void revokeInvitation(invitation.id)}
                  className="text-sm font-medium text-red-700 hover:underline disabled:opacity-50"
                >
                  Revoke et
                </button>
              </div>
            ))}
          </div>
        </section>
      ) : null}

      {showInviteDialog ? (
        <InviteMemberDialog
          organizationId={organization.id}
          actorRole={organization.role}
          onDismiss={() => setShowInviteDialog(false)}
        />
      ) : null}
    </section>
  );
}

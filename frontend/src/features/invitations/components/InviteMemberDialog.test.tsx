import { beforeEach, describe, expect, it, vi } from "vitest";
import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { renderWithProviders } from "@/test/renderWithProviders";
import { InviteMemberDialog } from "./InviteMemberDialog";

const mutateAsyncMock = vi.fn();

vi.mock("../hooks/useCreateInvitation", () => ({
  useCreateInvitation: () => ({ mutateAsync: mutateAsyncMock }),
}));

describe("InviteMemberDialog", () => {
  beforeEach(() => mutateAsyncMock.mockReset());

  it("validates and sends a member invitation", async () => {
    const user = userEvent.setup();
    const dismiss = vi.fn();
    mutateAsyncMock.mockResolvedValue({});
    renderWithProviders(
      <InviteMemberDialog organizationId="organization-1" actorRole="OWNER" onDismiss={dismiss} />,
    );

    await user.type(screen.getByLabelText("E-posta"), "member@example.com");
    await user.selectOptions(screen.getByLabelText("Organization rolü"), "ADMIN");
    await user.click(screen.getByRole("button", { name: "Davet gönder" }));

    expect(mutateAsyncMock).toHaveBeenCalledWith({ email: "member@example.com", role: "ADMIN" });
    expect(dismiss).toHaveBeenCalled();
  });

  it("does not offer OWNER role to an ADMIN actor", () => {
    renderWithProviders(
      <InviteMemberDialog organizationId="organization-1" actorRole="ADMIN" onDismiss={vi.fn()} />,
    );

    expect(screen.queryByRole("option", { name: "OWNER" })).not.toBeInTheDocument();
  });
});

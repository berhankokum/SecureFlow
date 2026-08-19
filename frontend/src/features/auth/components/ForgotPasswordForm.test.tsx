import { describe, it, expect, vi, beforeEach } from "vitest";
import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { renderWithProviders } from "@/test/renderWithProviders";
import { ForgotPasswordForm } from "./ForgotPasswordForm";

const forgotPasswordMock = vi.fn();

vi.mock("../api/forgotPassword", () => ({
  forgotPassword: (...args: unknown[]) => forgotPasswordMock(...args),
}));

describe("ForgotPasswordForm", () => {
  beforeEach(() => {
    forgotPasswordMock.mockReset();
  });

  it("requires a valid email", async () => {
    renderWithProviders(<ForgotPasswordForm />);
    const user = userEvent.setup();

    await user.click(screen.getByRole("button", { name: "Sıfırlama bağlantısı gönder" }));

    expect(await screen.findByText("E-posta zorunludur.")).toBeInTheDocument();
    expect(forgotPasswordMock).not.toHaveBeenCalled();
  });

  it("shows the same generic message regardless of whether the account exists", async () => {
    forgotPasswordMock.mockResolvedValueOnce(undefined);
    renderWithProviders(<ForgotPasswordForm />);
    const user = userEvent.setup();

    await user.type(screen.getByLabelText("E-posta"), "berhan@example.com");
    await user.click(screen.getByRole("button", { name: "Sıfırlama bağlantısı gönder" }));

    expect(
      await screen.findByText(/şifre sıfırlama bağlantısı gönderildi/i),
    ).toBeInTheDocument();
  });
});

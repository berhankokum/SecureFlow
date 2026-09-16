import { beforeEach, describe, expect, it, vi } from "vitest";
import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { renderWithProviders } from "@/test/renderWithProviders";
import { ApiError } from "@/lib/api/apiError";
import { ResetPasswordForm } from "./ResetPasswordForm";

const reset = vi.fn();
vi.mock("../hooks/useResetPassword", () => ({ useResetPassword: () => ({ mutateAsync: reset }) }));

describe("ResetPasswordForm", () => {
  beforeEach(() => { reset.mockReset(); });
  it("does not submit an invalid password", async () => {
    renderWithProviders(<ResetPasswordForm email="test@example.com" code="opaque-token" />);
    await userEvent.click(screen.getByRole("button", { name: "Şifreyi sıfırla" }));
    expect(reset).not.toHaveBeenCalled();
  });
  it("preserves the supplied token and draft after server rejection", async () => {
    reset.mockRejectedValue(new ApiError(400, "Bağlantının süresi dolmuş."));
    renderWithProviders(<ResetPasswordForm email="test@example.com" code="opaque-token" />);
    const user = userEvent.setup();
    await user.type(screen.getByLabelText("Yeni şifre"), "NewPassword123!");
    await user.click(screen.getByRole("button", { name: "Şifreyi sıfırla" }));
    expect(reset).toHaveBeenCalledWith({ email: "test@example.com", code: "opaque-token", newPassword: "NewPassword123!" });
    expect(await screen.findByText("Bağlantının süresi dolmuş.")).toBeVisible();
    expect(screen.getByLabelText("Yeni şifre")).toHaveValue("NewPassword123!");
    expect(reset).toHaveBeenCalledTimes(1);
  });
});

import { describe, it, expect, vi, beforeEach } from "vitest";
import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { renderWithProviders } from "@/test/renderWithProviders";
import { ApiError } from "@/lib/api/apiClient";
import { LoginForm } from "./LoginForm";

const loginMock = vi.fn();

vi.mock("../api/login", () => ({
  login: (...args: unknown[]) => loginMock(...args),
}));

// useLogin, başarılı girişten sonra gerçek bir /antiforgery/token isteği atar
// (bkz. useLogin.ts); component testinde bu ağ çağrısı no-op'a indirgenir.
vi.mock("@/lib/api/apiClient", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@/lib/api/apiClient")>();
  return { ...actual, refreshCsrfToken: vi.fn().mockResolvedValue(undefined) };
});

describe("LoginForm", () => {
  beforeEach(() => {
    loginMock.mockReset();
  });

  it("shows validation errors when submitted empty", async () => {
    renderWithProviders(<LoginForm />);
    const user = userEvent.setup();

    await user.click(screen.getByRole("button", { name: "Giriş Yap" }));

    expect(await screen.findByText("E-posta zorunludur.")).toBeInTheDocument();
    expect(await screen.findByText("Şifre zorunludur.")).toBeInTheDocument();
    expect(loginMock).not.toHaveBeenCalled();
  });

  it("submits credentials to the login mutation", async () => {
    loginMock.mockResolvedValueOnce(undefined);
    renderWithProviders(<LoginForm />);
    const user = userEvent.setup();

    await user.type(screen.getByLabelText("E-posta"), "berhan@example.com");
    await user.type(screen.getByLabelText("Şifre"), "Sup3rSecret!");
    await user.click(screen.getByRole("button", { name: "Giriş Yap" }));

    await waitFor(() => {
      expect(loginMock).toHaveBeenCalledTimes(1);
    });
    expect(loginMock.mock.calls[0][0]).toEqual({
      email: "berhan@example.com",
      password: "Sup3rSecret!",
    });
  });

  it("shows the backend error message on invalid credentials", async () => {
    loginMock.mockRejectedValueOnce(new ApiError(401, "E-posta veya şifre hatalı."));
    renderWithProviders(<LoginForm />);
    const user = userEvent.setup();

    await user.type(screen.getByLabelText("E-posta"), "berhan@example.com");
    await user.type(screen.getByLabelText("Şifre"), "wrong-password");
    await user.click(screen.getByRole("button", { name: "Giriş Yap" }));

    expect(await screen.findByText("E-posta veya şifre hatalı.")).toBeInTheDocument();
  });
});

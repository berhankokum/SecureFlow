import { describe, it, expect, vi, beforeEach } from "vitest";
import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { renderWithProviders } from "@/test/renderWithProviders";
import { ApiError } from "@/lib/api/apiClient";
import { RegisterForm } from "./RegisterForm";

const registerMock = vi.fn();

vi.mock("../api/register", () => ({
  register: (...args: unknown[]) => registerMock(...args),
}));

describe("RegisterForm", () => {
  beforeEach(() => {
    registerMock.mockReset();
  });

  it("shows validation errors for an invalid email and short password", async () => {
    renderWithProviders(<RegisterForm />);
    const user = userEvent.setup();

    await user.type(screen.getByLabelText("E-posta"), "not-an-email");
    await user.type(screen.getByLabelText("Şifre"), "short");
    await user.click(screen.getByRole("button", { name: "Kayıt Ol" }));

    expect(await screen.findByText("Geçerli bir e-posta girin.")).toBeInTheDocument();
    expect(await screen.findByText("Şifre en az 8 karakter olmalı.")).toBeInTheDocument();
    expect(registerMock).not.toHaveBeenCalled();
  });

  it("shows a success message after registering", async () => {
    registerMock.mockResolvedValueOnce(undefined);
    renderWithProviders(<RegisterForm />);
    const user = userEvent.setup();

    await user.type(screen.getByLabelText("E-posta"), "berhan@example.com");
    await user.type(screen.getByLabelText("Şifre"), "Sup3rSecret!");
    await user.click(screen.getByRole("button", { name: "Kayıt Ol" }));

    expect(await screen.findByText(/Hesabınız oluşturuldu/)).toBeInTheDocument();
  });

  it("maps backend field validation errors (e.g. duplicate email) onto the form", async () => {
    registerMock.mockRejectedValueOnce(
      new ApiError(400, "One or more validation errors occurred.", {
        Email: ["Bu e-posta zaten kayıtlı."],
      }),
    );
    renderWithProviders(<RegisterForm />);
    const user = userEvent.setup();

    await user.type(screen.getByLabelText("E-posta"), "berhan@example.com");
    await user.type(screen.getByLabelText("Şifre"), "Sup3rSecret!");
    await user.click(screen.getByRole("button", { name: "Kayıt Ol" }));

    await waitFor(() => {
      expect(screen.getByText("Bu e-posta zaten kayıtlı.")).toBeInTheDocument();
    });
  });
});

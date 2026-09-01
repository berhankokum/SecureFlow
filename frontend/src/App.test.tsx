import { beforeEach, describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import App from "./App";

const refreshCsrfTokenMock = vi.fn();

vi.mock("@/lib/api/apiClient", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@/lib/api/apiClient")>();
  return { ...actual, refreshCsrfToken: () => refreshCsrfTokenMock() };
});

vi.mock("react-router-dom", async (importOriginal) => {
  const actual = await importOriginal<typeof import("react-router-dom")>();
  return { ...actual, RouterProvider: () => <div>Application routes</div> };
});

describe("App security bootstrap", () => {
  beforeEach(() => {
    refreshCsrfTokenMock.mockReset();
  });

  it("does not mount application routes before the CSRF token is ready", async () => {
    let resolveToken!: () => void;
    refreshCsrfTokenMock.mockReturnValueOnce(new Promise<void>((resolve) => {
      resolveToken = resolve;
    }));

    render(<App />);

    expect(screen.getByText("Güvenli oturum hazırlanıyor...")).toBeInTheDocument();
    expect(screen.queryByText("Application routes")).not.toBeInTheDocument();

    resolveToken();

    expect(await screen.findByText("Application routes")).toBeInTheDocument();
  });

  it("shows a retry state when the CSRF bootstrap fails", async () => {
    refreshCsrfTokenMock
      .mockRejectedValueOnce(new Error("network failure"))
      .mockResolvedValueOnce(undefined);

    render(<App />);

    const retry = await screen.findByRole("button", { name: "Tekrar dene" });
    await userEvent.click(retry);

    expect(await screen.findByText("Application routes")).toBeInTheDocument();
  });
});

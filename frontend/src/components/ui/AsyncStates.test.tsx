import { describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { LoadingState } from "./LoadingState";
import { ErrorState } from "./ErrorState";

describe("async UI states", () => {
  it("announces loading once while hiding decorative skeletons", () => {
    const { container } = render(<LoadingState label="Projects yükleniyor..." />);
    expect(screen.getByRole("status")).toHaveTextContent("Projects yükleniyor...");
    expect(container.querySelector('[aria-hidden="true"]')).toHaveClass("motion-safe:animate-pulse");
  });

  it("supports keyboard retry and prevents repeat requests while fetching", async () => {
    const retry = vi.fn();
    const user = userEvent.setup();
    const { rerender } = render(<ErrorState message="Yüklenemedi." onRetry={retry} />);
    expect(screen.getByRole("alert")).toHaveTextContent("Yüklenemedi.");
    await user.tab();
    await user.keyboard("{Enter}");
    expect(retry).toHaveBeenCalledOnce();
    rerender(<ErrorState message="Yüklenemedi." onRetry={retry} retrying />);
    expect(screen.getByRole("button")).toBeDisabled();
  });
});

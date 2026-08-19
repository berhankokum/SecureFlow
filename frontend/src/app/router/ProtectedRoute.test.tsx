import { describe, it, expect, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import { createMemoryRouter, RouterProvider } from "react-router-dom";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { ProtectedRoute } from "./ProtectedRoute";

const useCurrentUserMock = vi.fn();

vi.mock("@/features/auth/hooks/useCurrentUser", () => ({
  useCurrentUser: () => useCurrentUserMock(),
}));

function renderProtectedRoute() {
  const queryClient = new QueryClient();
  const router = createMemoryRouter(
    [
      { path: "/login", element: <div>Login Page</div> },
      {
        path: "/",
        element: <ProtectedRoute />,
        children: [{ index: true, element: <div>Protected Content</div> }],
      },
    ],
    { initialEntries: ["/"] },
  );

  return render(
    <QueryClientProvider client={queryClient}>
      <RouterProvider router={router} />
    </QueryClientProvider>,
  );
}

describe("ProtectedRoute", () => {
  it("shows a loading state while the auth check is in flight", () => {
    useCurrentUserMock.mockReturnValue({ data: undefined, isLoading: true, isError: false });

    renderProtectedRoute();

    expect(screen.getByText("Yükleniyor...")).toBeInTheDocument();
  });

  it("redirects to /login when the user is not authenticated", async () => {
    useCurrentUserMock.mockReturnValue({ data: undefined, isLoading: false, isError: true });

    renderProtectedRoute();

    expect(await screen.findByText("Login Page")).toBeInTheDocument();
  });

  it("renders the protected content when authenticated", async () => {
    useCurrentUserMock.mockReturnValue({
      data: { id: "1", email: "berhan@example.com", emailConfirmed: true },
      isLoading: false,
      isError: false,
    });

    renderProtectedRoute();

    expect(await screen.findByText("Protected Content")).toBeInTheDocument();
  });
});

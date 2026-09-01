import { beforeEach, describe, expect, it, vi } from "vitest";
import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { renderWithProviders } from "@/test/renderWithProviders";
import { CreateOrganizationForm } from "./CreateOrganizationForm";

const mutateAsyncMock = vi.fn();
const navigateMock = vi.fn();

vi.mock("../hooks/useCreateOrganization", () => ({
  useCreateOrganization: () => ({ mutateAsync: mutateAsyncMock }),
}));

vi.mock("react-router-dom", async (importOriginal) => {
  const actual = await importOriginal<typeof import("react-router-dom")>();
  return { ...actual, useNavigate: () => navigateMock };
});

describe("CreateOrganizationForm", () => {
  beforeEach(() => {
    mutateAsyncMock.mockReset();
    navigateMock.mockReset();
  });

  it("creates the organization and enters its tenant route", async () => {
    const user = userEvent.setup();
    mutateAsyncMock.mockResolvedValue({ slug: "acme-software" });
    renderWithProviders(<CreateOrganizationForm />);

    await user.type(screen.getByLabelText("Organization adı"), "ACME Software");
    await user.click(screen.getByRole("button", { name: "Organization oluştur" }));

    expect(mutateAsyncMock).toHaveBeenCalledWith({
      name: "ACME Software",
      logoUrl: undefined,
    });
    expect(navigateMock).toHaveBeenCalledWith("/app/acme-software/dashboard", { replace: true });
  });

  it("rejects an invalid logo URL before sending a request", async () => {
    const user = userEvent.setup();
    renderWithProviders(<CreateOrganizationForm />);

    await user.type(screen.getByLabelText("Organization adı"), "ACME");
    await user.type(screen.getByLabelText(/Logo adresi/), "javascript:alert(1)");
    await user.click(screen.getByRole("button", { name: "Organization oluştur" }));

    expect(await screen.findByText("Logo adresi http:// veya https:// ile başlamalıdır.")).toBeInTheDocument();
    expect(mutateAsyncMock).not.toHaveBeenCalled();
  });
});

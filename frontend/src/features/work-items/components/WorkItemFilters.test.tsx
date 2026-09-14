import { describe, expect, it, vi } from "vitest";
import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { useLocation, useNavigate } from "react-router-dom";
import { renderWithProviders } from "@/test/renderWithProviders";
import type { ProjectDetails } from "@/features/projects/api/types";
import { WorkItemFilters } from "./WorkItemFilters";

vi.mock("../hooks/useWorkItems", () => ({ useWorkflow: () => ({ data: { states: [{ id: "state", name: "TODO" }] } }) }));
vi.mock("@/features/labels/hooks/useLabels", () => ({ useLabels: () => ({ data: [{ id: "label", name: "Security" }] }) }));
const project = { id: "project", members: [] } as unknown as ProjectDetails;
function Location() {
  const location = useLocation();
  const navigate = useNavigate();
  return <><output aria-label="URL">{location.search}</output><button onClick={() => navigate(-1)}>Geri</button></>;
}

describe("WorkItemFilters", () => {
  it("restores URL state, combines filters, resets page and preserves unrelated parameters", async () => {
    const user = userEvent.setup();
    renderWithProviders(<><WorkItemFilters project={project} /><Location /></>, { route: "/?search=tenant&page=3&tab=details" });
    expect(screen.getByLabelText("Arama")).toHaveValue("tenant");
    await user.selectOptions(screen.getByLabelText("Etiket filtresi"), "label");
    await user.selectOptions(screen.getByLabelText("Öncelik filtresi"), "HIGH");
    await user.click(screen.getByRole("button", { name: "Filtreleri uygula" }));
    const params = new URLSearchParams(screen.getByLabelText("URL").textContent!);
    expect(params.get("search")).toBe("tenant");
    expect(params.get("label")).toBe("label");
    expect(params.get("priority")).toBe("HIGH");
    expect(params.get("page")).toBeNull();
    expect(params.get("tab")).toBe("details");
    await user.click(screen.getByText("Geri"));
    expect(screen.getByLabelText("URL")).toHaveTextContent("page=3");
    expect(screen.getByLabelText("Öncelik filtresi")).toHaveValue("");
    await user.click(screen.getByRole("button", { name: "Filtreleri temizle" }));
    expect(screen.getByLabelText("URL")).toHaveTextContent("?tab=details");
    expect(screen.getByLabelText("Arama")).toHaveValue("");
  });

  it("rejects reversed dates before submitting", async () => {
    const user = userEvent.setup();
    renderWithProviders(<><WorkItemFilters project={project} /><Location /></>, { route: "/?dueFrom=2026-09-20&dueTo=2026-09-01" });
    await user.click(screen.getByRole("button", { name: "Filtreleri uygula" }));
    expect(screen.getByRole("alert")).toHaveTextContent("Başlangıç tarihi");
  });
});

import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { apiClient } from "./apiClient";

describe("API transport contract", () => {
  const fetchMock = vi.fn();
  beforeEach(() => {
    document.cookie = "XSRF-TOKEN=; Max-Age=0; Path=/";
    vi.stubGlobal("fetch", fetchMock);
    fetchMock.mockReset();
  });
  afterEach(() => {
    document.cookie = "XSRF-TOKEN=; Max-Age=0; Path=/";
    vi.unstubAllGlobals();
  });

  it("reads with cookies but without an unnecessary CSRF bootstrap", async () => {
    fetchMock.mockResolvedValue(new Response(JSON.stringify({ id: "one" })));
    expect(await apiClient.get("/resource")).toEqual({ id: "one" });
    expect(fetchMock).toHaveBeenCalledTimes(1);
    const [, options] = fetchMock.mock.calls[0];
    expect(options.credentials).toBe("include");
    expect(options.headers.has("X-CSRF-TOKEN")).toBe(false);
  });

  it.each(["post", "put", "patch", "delete"] as const)("%s requires CSRF and sends the cookie token", async method => {
    document.cookie = "XSRF-TOKEN=csrf-token; Path=/";
    fetchMock.mockResolvedValue(new Response(null, { status: 204 }));
    if (method === "delete") await apiClient.delete("/resource", 7);
    else await apiClient[method]("/resource", { title: "safe" });
    const [, options] = fetchMock.mock.calls[0];
    expect(options.method).toBe(method.toUpperCase());
    expect(options.headers.get("X-CSRF-TOKEN")).toBe("csrf-token");
    expect(options.credentials).toBe("include");
    if (method === "delete") expect(options.headers.get("If-Match")).toBe('"7"');
  });

  it("does not send a mutation when CSRF bootstrap fails", async () => {
    fetchMock.mockResolvedValue(new Response(JSON.stringify({ title: "CSRF unavailable" }), { status: 503 }));
    await expect(apiClient.post("/resource", {})).rejects.toMatchObject({ status: 503 });
    expect(fetchMock).toHaveBeenCalledTimes(1);
    expect(fetchMock.mock.calls[0][0]).toContain("/antiforgery/token");
  });

  it("does not retry a stale write and preserves validation/trace information", async () => {
    document.cookie = "XSRF-TOKEN=csrf-token; Path=/";
    const trace = "0123456789abcdef0123456789abcdef";
    fetchMock.mockResolvedValue(new Response(JSON.stringify({ title: "Stale write", errors: { title: ["Reload"] } }),
      { status: 409, headers: { "X-Trace-Id": trace } }));
    await expect(apiClient.patch("/resource", { title: "draft" }, 9)).rejects.toMatchObject({ status: 409, traceId: trace, errors: { title: ["Reload"] } });
    expect(fetchMock).toHaveBeenCalledTimes(1);
    expect(fetchMock.mock.calls[0][1].headers.get("If-Match")).toBe('"9"');
  });
});

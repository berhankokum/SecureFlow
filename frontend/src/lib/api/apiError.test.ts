import { describe, expect, it } from "vitest";
import { ApiError, parseErrorResponse } from "./apiError";

describe("TraceId error correlation", () => {
  const traceId = "0123456789abcdef0123456789abcdef";
  it("uses a validated response header for JSON and non-JSON errors", async () => {
    for (const body of [JSON.stringify({ title: "Hata", traceId: "invalid" }), "not-json"]) {
      const error = await parseErrorResponse(new Response(body, { status: 500, headers: { "X-Trace-Id": traceId } }));
      expect(error.traceId).toBe(traceId);
      expect(error.message).toContain(`Referans: ${traceId}`);
    }
  });
  it("falls back to ProblemDetails traceId and preserves field errors", async () => {
    const error = await parseErrorResponse(new Response(JSON.stringify({ title: "Validation", traceId, errors: { name: ["Required"] } }), { status: 400 }));
    expect(error.traceId).toBe(traceId);
    expect(error.errors).toEqual({ name: ["Required"] });
  });
  it("does not reflect arbitrary trace header content", () => {
    const error = new ApiError(500, "Hata", undefined, "<script>secret</script>");
    expect(error.traceId).toBeUndefined();
    expect(error.message).toBe("Hata");
  });
});

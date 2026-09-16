import { describe, expect, it } from "vitest";
import { ApiError, parseErrorResponse } from "./apiError";

describe("TraceId error correlation", () => {
  it("shows bounded retry information on 429 without reflecting an arbitrary header", async () => {
    const error = await parseErrorResponse(new Response("", { status: 429, headers: { "Retry-After": "60" } }));
    expect(error.retryAfterSeconds).toBe(60);
    expect(error.message).toContain("60 saniye sonra");
    for (const value of ["<script>alert(1)</script>", "-1", "999999", "1.5"]) {
      const invalid = await parseErrorResponse(new Response("", { status: 429, headers: { "Retry-After": value } }));
      expect(invalid.retryAfterSeconds).toBeUndefined();
      expect(invalid.message).toBe("Çok fazla istek gönderdiniz.");
    }
  });
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

export type ValidationErrors = Record<string, string[]>;

export class ApiError extends Error {
  readonly status: number;
  readonly errors?: ValidationErrors;
  readonly traceId?: string;

  constructor(status: number, message: string, errors?: ValidationErrors, traceId?: string) {
    const validTraceId = traceId && /^[a-f0-9]{32}$/i.test(traceId) ? traceId : undefined;
    super(validTraceId ? `${message} (Referans: ${validTraceId})` : message);
    this.status = status;
    this.errors = errors;
    this.traceId = validTraceId;
  }
}

/**
 * Backend ProblemDetails / ValidationProblemDetails (SecureFlow-Final.md §32) şeklini bekler:
 * { title, status, errors?: { field: string[] } }
 */
export async function parseErrorResponse(response: Response): Promise<ApiError> {
  let title = `İstek başarısız oldu (${response.status}).`;
  let errors: ValidationErrors | undefined;
  let traceId = response.headers.get("X-Trace-Id") ?? undefined;

  try {
    const body = await response.json();
    if (!traceId && typeof body?.traceId === "string") traceId = body.traceId;
    if (typeof body?.title === "string") {
      title = body.title;
    }
    if (body?.errors && typeof body.errors === "object") {
      errors = body.errors as ValidationErrors;
    }
  } catch {
    // Body JSON değilse varsayılan mesaj kullanılır.
  }

  return new ApiError(response.status, title, errors, traceId);
}

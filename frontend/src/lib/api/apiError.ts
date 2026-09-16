export type ValidationErrors = Record<string, string[]>;

export class ApiError extends Error {
  readonly status: number;
  readonly errors?: ValidationErrors;
  readonly traceId?: string;
  readonly retryAfterSeconds?: number;

  constructor(status: number, message: string, errors?: ValidationErrors, traceId?: string, retryAfterSeconds?: number) {
    const validTraceId = traceId && /^[a-f0-9]{32}$/i.test(traceId) ? traceId : undefined;
    const retry = status === 429 && Number.isInteger(retryAfterSeconds) && retryAfterSeconds! > 0 && retryAfterSeconds! <= 3600
      ? retryAfterSeconds : undefined;
    const readableMessage = retry ? `${message} ${retry} saniye sonra tekrar deneyin.` : message;
    super(validTraceId ? `${readableMessage} (Referans: ${validTraceId})` : readableMessage);
    this.status = status;
    this.errors = errors;
    this.traceId = validTraceId;
    this.retryAfterSeconds = retry;
  }
}

/**
 * Backend ProblemDetails / ValidationProblemDetails (SecureFlow-Final.md §32) şeklini bekler:
 * { title, status, errors?: { field: string[] } }
 */
export async function parseErrorResponse(response: Response): Promise<ApiError> {
  let title = response.status === 429 ? "Çok fazla istek gönderdiniz." : `İstek başarısız oldu (${response.status}).`;
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

  const retryHeader = response.headers.get("Retry-After");
  const retry = retryHeader && /^\d{1,4}$/.test(retryHeader) ? Number(retryHeader) : undefined;
  return new ApiError(response.status, title, errors, traceId, retry);
}

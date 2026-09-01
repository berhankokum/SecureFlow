export type ValidationErrors = Record<string, string[]>;

export class ApiError extends Error {
  readonly status: number;
  readonly errors?: ValidationErrors;

  constructor(status: number, message: string, errors?: ValidationErrors) {
    super(message);
    this.status = status;
    this.errors = errors;
  }
}

/**
 * Backend ProblemDetails / ValidationProblemDetails (SecureFlow-Final.md §32) şeklini bekler:
 * { title, status, errors?: { field: string[] } }
 */
export async function parseErrorResponse(response: Response): Promise<ApiError> {
  let title = `İstek başarısız oldu (${response.status}).`;
  let errors: ValidationErrors | undefined;

  try {
    const body = await response.json();
    if (typeof body?.title === "string") {
      title = body.title;
    }
    if (body?.errors && typeof body.errors === "object") {
      errors = body.errors as ValidationErrors;
    }
  } catch {
    // Body JSON değilse varsayılan mesaj kullanılır.
  }

  return new ApiError(response.status, title, errors);
}

import { env } from "@/app/config/env";
import { ApiError, parseErrorResponse } from "./apiError";

const SAFE_METHODS = new Set(["GET", "HEAD", "OPTIONS"]);

function readCookie(name: string): string | null {
  const match = document.cookie.match(new RegExp(`(?:^|; )${name}=([^;]*)`));
  return match ? decodeURIComponent(match[1]) : null;
}

/**
 * ASP.NET Core antiforgery token'ı kimlik doğrulanmış principal'a bağlıdır:
 * login/logout sonrası eski token geçersiz kalır, bu yüzden her authentication
 * state değişiminden sonra yeniden istenmelidir (bkz. AuthController manuel testi).
 */
export async function refreshCsrfToken(): Promise<void> {
  const response = await fetch(`${env.apiBaseUrl}/antiforgery/token`, { credentials: "include" });
  if (!response.ok) {
    throw await parseErrorResponse(response);
  }
}

interface RequestOptions {
  method?: string;
  body?: unknown;
}

async function request<TResponse>(path: string, options: RequestOptions = {}): Promise<TResponse> {
  const method = (options.method ?? "GET").toUpperCase();
  const headers = new Headers();

  if (options.body !== undefined) {
    headers.set("Content-Type", "application/json");
  }

  if (!SAFE_METHODS.has(method)) {
    let csrfToken = readCookie("XSRF-TOKEN");
    if (!csrfToken) {
      await refreshCsrfToken();
      csrfToken = readCookie("XSRF-TOKEN");
    }

    if (csrfToken) {
      headers.set("X-CSRF-TOKEN", csrfToken);
    } else {
      throw new ApiError(0, "Güvenli istek doğrulaması başlatılamadı.");
    }
  }

  const response = await fetch(`${env.apiBaseUrl}${path}`, {
    method,
    headers,
    credentials: "include",
    body: options.body !== undefined ? JSON.stringify(options.body) : undefined,
  });

  if (!response.ok) {
    throw await parseErrorResponse(response);
  }

  if (response.status === 204) {
    return undefined as TResponse;
  }

  return (await response.json()) as TResponse;
}

export const apiClient = {
  get: <T>(path: string) => request<T>(path),
  post: <T>(path: string, body?: unknown) => request<T>(path, { method: "POST", body }),
  patch: <T>(path: string, body: unknown) => request<T>(path, { method: "PATCH", body }),
  delete: <T>(path: string) => request<T>(path, { method: "DELETE" }),
};

export { ApiError };

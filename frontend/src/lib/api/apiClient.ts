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

async function getCsrfToken(): Promise<string> {
  let csrfToken = readCookie("XSRF-TOKEN");
  if (!csrfToken) {
    await refreshCsrfToken();
    csrfToken = readCookie("XSRF-TOKEN");
  }

  if (!csrfToken) {
    throw new ApiError(0, "Güvenli istek doğrulaması başlatılamadı.");
  }

  return csrfToken;
}

interface RequestOptions {
  version?: number;
  method?: string;
  body?: unknown;
}

async function request<TResponse>(path: string, options: RequestOptions = {}): Promise<TResponse> {
  const method = (options.method ?? "GET").toUpperCase();
  const headers = new Headers();
  if (options.version !== undefined) headers.set("If-Match", `"${options.version}"`);

  if (options.body !== undefined) {
    headers.set("Content-Type", "application/json");
  }

  if (!SAFE_METHODS.has(method)) {
    headers.set("X-CSRF-TOKEN", await getCsrfToken());
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

async function upload<TResponse>(
  path: string,
  file: File,
  onProgress?: (percent: number) => void,
): Promise<TResponse> {
  const csrfToken = await getCsrfToken();
  return new Promise<TResponse>((resolve, reject) => {
    const xhr = new XMLHttpRequest();
    xhr.open("POST", `${env.apiBaseUrl}${path}`);
    xhr.withCredentials = true;
    xhr.setRequestHeader("X-CSRF-TOKEN", csrfToken);
    xhr.upload.onprogress = event => {
      if (event.lengthComputable) {
        onProgress?.(Math.min(100, Math.round((event.loaded / event.total) * 100)));
      }
    };
    xhr.onerror = () => reject(new ApiError(0, "Dosya yükleme bağlantısı kurulamadı."));
    xhr.onload = () => {
      if (xhr.status >= 200 && xhr.status < 300) {
        try {
          resolve(JSON.parse(xhr.responseText) as TResponse);
        } catch {
          reject(new ApiError(xhr.status, "Sunucudan geçersiz yükleme yanıtı alındı."));
        }
        return;
      }

      if (xhr.status === 0) {
        reject(new ApiError(0, "Dosya yükleme bağlantısı kurulamadı."));
        return;
      }

      void parseErrorResponse(new Response(xhr.responseText, {
        status: xhr.status,
        headers: { "Content-Type": xhr.getResponseHeader("Content-Type") ?? "application/problem+json", "X-Trace-Id": xhr.getResponseHeader("X-Trace-Id") ?? "", "Retry-After": xhr.getResponseHeader("Retry-After") ?? "" },
      })).then(reject);
    };
    const form = new FormData();
    form.append("file", file, file.name);
    xhr.send(form);
  });
}

async function download(path: string, fileName: string): Promise<void> {
  const response = await fetch(`${env.apiBaseUrl}${path}`, { credentials: "include" });
  if (!response.ok) {
    throw await parseErrorResponse(response);
  }

  const objectUrl = URL.createObjectURL(await response.blob());
  try {
    const anchor = document.createElement("a");
    anchor.href = objectUrl;
    anchor.download = fileName;
    anchor.click();
  } finally {
    URL.revokeObjectURL(objectUrl);
  }
}

export const apiClient = {
  get: <T>(path: string) => request<T>(path),
  post: <T>(path: string, body?: unknown, version?: number) => request<T>(path, { method: "POST", body, version }),
  put: <T>(path: string, body: unknown) => request<T>(path, { method: "PUT", body }),
  patch: <T>(path: string, body: unknown, version?: number) => request<T>(path, { method: "PATCH", body, version }),
  delete: <T>(path: string, version?: number) => request<T>(path, { method: "DELETE", version }),
  upload,
  download,
};

export { ApiError };

/**
 * Frontend'e yalnızca public configuration verilir (SecureFlow-Final.md §43).
 * Secret değerler (DB password, API secret, S3 secret, SMTP password) burada bulunmaz.
 */
export const env = {
  apiBaseUrl: import.meta.env.VITE_API_BASE_URL ?? "/api/v1",
} as const;

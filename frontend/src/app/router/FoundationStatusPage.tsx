import { useQuery } from "@tanstack/react-query";

type HealthState = "checking" | "healthy" | "unreachable";

async function pingBackend(): Promise<HealthState> {
  try {
    const response = await fetch("/health/ready");
    return response.ok ? "healthy" : "unreachable";
  } catch {
    return "unreachable";
  }
}

/**
 * FAZ 1 geçici sayfası: Login/Dashboard gibi gerçek feature sayfaları
 * FAZ 2+'ta gelene kadar, backend/frontend/altyapının uçtan uca bağlı
 * olduğunu doğrulamak için kullanılır.
 */
export function FoundationStatusPage() {
  const { data: status } = useQuery({
    queryKey: ["foundation-health"],
    queryFn: pingBackend,
  });

  return (
    <div className="mx-auto max-w-xl space-y-4">
      <h1 className="text-2xl font-semibold">Foundation</h1>
      <p className="text-sm text-slate-600">
        FAZ 1 — Repository ve Project Foundation iskeleti çalışıyor.
      </p>
      <div className="rounded-md border border-slate-200 bg-white p-4">
        <span className="text-sm font-medium">API bağlantısı: </span>
        <span
          className={
            status === "healthy"
              ? "text-emerald-600"
              : status === "unreachable"
                ? "text-red-600"
                : "text-slate-500"
          }
        >
          {status ?? "checking"}
        </span>
      </div>
    </div>
  );
}

import { useEffect, useState } from "react";
import { RouterProvider } from "react-router-dom";
import { QueryProvider } from "@/app/providers/QueryProvider";
import { router } from "@/app/router/router";
import { refreshCsrfToken } from "@/lib/api/apiClient";

function App() {
  const [csrfStatus, setCsrfStatus] = useState<"loading" | "ready" | "error">("loading");
  const [bootstrapAttempt, setBootstrapAttempt] = useState(0);

  useEffect(() => {
    let cancelled = false;

    refreshCsrfToken()
      .then(() => {
        if (!cancelled) {
          setCsrfStatus("ready");
        }
      })
      .catch(() => {
        if (!cancelled) {
          setCsrfStatus("error");
        }
      });

    return () => {
      cancelled = true;
    };
  }, [bootstrapAttempt]);

  if (csrfStatus === "loading") {
    return <div className="p-6 text-sm text-slate-600">Güvenli oturum hazırlanıyor...</div>;
  }

  if (csrfStatus === "error") {
    return (
      <div className="p-6 text-sm text-red-700">
        <p>Güvenli oturum başlatılamadı.</p>
        <button
          type="button"
          className="mt-3 rounded-md bg-slate-900 px-3 py-2 text-white"
          onClick={() => {
            setCsrfStatus("loading");
            setBootstrapAttempt((attempt) => attempt + 1);
          }}
        >
          Tekrar dene
        </button>
      </div>
    );
  }

  return (
    <QueryProvider>
      <RouterProvider router={router} />
    </QueryProvider>
  );
}

export default App;

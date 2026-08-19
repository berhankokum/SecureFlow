import { useEffect } from "react";
import { RouterProvider } from "react-router-dom";
import { QueryProvider } from "@/app/providers/QueryProvider";
import { router } from "@/app/router/router";
import { refreshCsrfToken } from "@/lib/api/apiClient";

function App() {
  useEffect(() => {
    void refreshCsrfToken();
  }, []);

  return (
    <QueryProvider>
      <RouterProvider router={router} />
    </QueryProvider>
  );
}

export default App;

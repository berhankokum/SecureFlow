export function ErrorState({ message, onRetry, retrying = false }: {
  message: string;
  onRetry: () => void;
  retrying?: boolean;
}) {
  return <div className="my-4 rounded-lg border border-red-200 bg-red-50 p-4 text-sm text-red-700">
    <p role="alert">{message}</p>
    <button type="button" onClick={onRetry} disabled={retrying}
      className="mt-3 rounded-md border border-red-300 bg-white px-3 py-2 font-medium disabled:opacity-50">
      {retrying ? "Yeniden deneniyor..." : "Yeniden dene"}
    </button>
  </div>;
}

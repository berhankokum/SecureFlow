import { useEffect, useRef, type ReactNode } from "react";

/** Native modal supplies focus containment and makes the background inert. */
export function Dialog({ labelledBy, onDismiss, children }: {
  labelledBy: string;
  onDismiss: () => void;
  children: ReactNode;
}) {
  const ref = useRef<HTMLDialogElement>(null);

  useEffect(() => {
    const dialog = ref.current!;
    const trigger = document.activeElement instanceof HTMLElement ? document.activeElement : null;
    dialog.showModal();
    dialog.querySelector<HTMLElement>("[data-dialog-autofocus]")?.focus();
    return () => {
      dialog.close();
      if (trigger?.isConnected) trigger.focus();
    };
  }, []);

  return <dialog ref={ref} aria-labelledby={labelledBy}
    className="m-auto max-h-[calc(100dvh-2rem)] w-[calc(100%-2rem)] max-w-md overflow-y-auto rounded-xl border border-slate-200 bg-white p-5 text-slate-900 shadow-xl backdrop:bg-slate-950/40 sm:p-6"
    onCancel={event => { event.preventDefault(); onDismiss(); }}
    onKeyDown={event => {
      if (event.key !== "Tab") return;
      const targets = [...event.currentTarget.querySelectorAll<HTMLElement>(
        'button:not(:disabled), a[href], input:not(:disabled), select:not(:disabled), textarea:not(:disabled), [tabindex="0"]',
      )].filter(element => element.tabIndex >= 0 && element.getClientRects().length > 0);
      const first = targets[0], last = targets.at(-1);
      if (!first || !last) { event.preventDefault(); event.currentTarget.focus(); return; }
      if (event.shiftKey && document.activeElement === first) { event.preventDefault(); last.focus(); }
      else if (!event.shiftKey && document.activeElement === last) { event.preventDefault(); first.focus(); }
    }}>
    {children}
  </dialog>;
}

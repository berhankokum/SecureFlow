import { createContext } from "react";

export type RealtimeStatus = "connecting" | "connected" | "reconnecting" | "disconnected";

export interface RealtimeContextValue {
  status: RealtimeStatus;
  subscribeProject: (projectId: string) => () => void;
}

export const RealtimeContext = createContext<RealtimeContextValue | null>(null);


import { useContext, useEffect } from "react";
import { RealtimeContext } from "./realtimeContext";

export function useRealtimeProject(projectId: string) {
  const realtime = useContext(RealtimeContext);
  useEffect(() => {
    if (!realtime || projectId.length === 0) return;
    return realtime.subscribeProject(projectId);
  }, [projectId, realtime]);
}


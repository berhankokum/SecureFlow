import {
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
  type HubConnection,
} from "@microsoft/signalr";
import { useQueryClient } from "@tanstack/react-query";
import {
  useCallback,
  useEffect,
  useMemo,
  useRef,
  useState,
  type PropsWithChildren,
} from "react";
import { activityKeys, commentKeys } from "@/features/comments/hooks/useCollaboration";
import { notificationKeys } from "@/features/notifications/hooks/useNotifications";
import { projectKeys } from "@/features/projects/hooks/useProjects";
import { workItemKeys } from "@/features/work-items/hooks/useWorkItems";
import { RealtimeContext, type RealtimeStatus } from "./realtimeContext";

interface WorkItemUpdatedEvent {
  eventId: string;
  organizationId: string;
  projectId: string;
  workItemId: string;
}

interface CommentCreatedEvent extends WorkItemUpdatedEvent {
  commentId: string;
}

interface NotificationCreatedEvent {
  eventId: string;
  notificationId: string;
}

const reconnectDelays = [0, 2_000, 5_000, 10_000, 30_000];
const processedEventLimit = 1_000;

function rememberEvent(processed: Set<string>, eventId: unknown): boolean {
  if (typeof eventId !== "string" || eventId.length === 0 || processed.has(eventId)) {
    return false;
  }

  processed.add(eventId);
  if (processed.size > processedEventLimit) {
    const oldest = processed.values().next().value;
    if (oldest) processed.delete(oldest);
  }
  return true;
}

async function invokeSafely(connection: HubConnection, method: string, id: string) {
  if (connection.state !== HubConnectionState.Connected || id.length === 0) return;
  try {
    await connection.invoke(method, id);
  } catch {
    // Server-side authorization is authoritative. A stale route may lose access at any time.
  }
}

export function RealtimeProvider({
  organizationId,
  children,
}: PropsWithChildren<{ organizationId: string }>) {
  const queryClient = useQueryClient();
  const connectionRef = useRef<HubConnection | null>(null);
  const desiredProjectsRef = useRef(new Set<string>());
  const [status, setStatus] = useState<RealtimeStatus>("connecting");

  const refetchAfterReconnect = useCallback(() => {
    void Promise.all([
      queryClient.invalidateQueries({ queryKey: projectKeys.all(organizationId) }),
      queryClient.invalidateQueries({ queryKey: ["work-items"] }),
      queryClient.invalidateQueries({ queryKey: ["comments"] }),
      queryClient.invalidateQueries({ queryKey: ["activity"] }),
      queryClient.invalidateQueries({ queryKey: notificationKeys.list }),
    ]);
  }, [organizationId, queryClient]);

  useEffect(() => {
    let disposed = false;
    const processedEvents = new Set<string>();
    const connection = new HubConnectionBuilder()
      .withUrl("/hubs/realtime", { withCredentials: true })
      .withAutomaticReconnect(reconnectDelays)
      .configureLogging(LogLevel.Warning)
      .build();
    connectionRef.current = connection;

    connection.on("WorkItemUpdated", (message: WorkItemUpdatedEvent) => {
      if (message?.organizationId !== organizationId
          || !rememberEvent(processedEvents, message?.eventId)) return;
      void Promise.all([
        queryClient.invalidateQueries({ queryKey: workItemKeys.all(message.projectId) }),
        queryClient.invalidateQueries({ queryKey: workItemKeys.detail(message.workItemId) }),
        queryClient.invalidateQueries({ queryKey: activityKeys.all(message.workItemId) }),
      ]);
    });
    connection.on("CommentCreated", (message: CommentCreatedEvent) => {
      if (message?.organizationId !== organizationId
          || !rememberEvent(processedEvents, message?.eventId)) return;
      void Promise.all([
        queryClient.invalidateQueries({ queryKey: commentKeys.all(message.workItemId) }),
        queryClient.invalidateQueries({ queryKey: activityKeys.all(message.workItemId) }),
      ]);
    });
    connection.on("NotificationCreated", (message: NotificationCreatedEvent) => {
      if (!rememberEvent(processedEvents, message?.eventId)) return;
      void queryClient.invalidateQueries({ queryKey: notificationKeys.list });
    });

    connection.onreconnecting(() => {
      if (!disposed) setStatus("reconnecting");
    });
    connection.onreconnected(async () => {
      if (disposed) return;
      setStatus("connected");
      await invokeSafely(connection, "SubscribeOrganization", organizationId);
      await Promise.all(
        [...desiredProjectsRef.current].map(projectId =>
          invokeSafely(connection, "SubscribeProject", projectId)),
      );
      refetchAfterReconnect();
    });
    connection.onclose(() => {
      if (!disposed) setStatus("disconnected");
    });

    const start = async () => {
      setStatus("connecting");
      try {
        await connection.start();
        if (disposed) {
          await connection.stop();
          return;
        }
        setStatus("connected");
        await invokeSafely(connection, "SubscribeOrganization", organizationId);
        await Promise.all(
          [...desiredProjectsRef.current].map(projectId =>
            invokeSafely(connection, "SubscribeProject", projectId)),
        );
      } catch {
        if (!disposed) setStatus("disconnected");
      }
    };
    void start();

    return () => {
      disposed = true;
      connectionRef.current = null;
      void connection.stop();
    };
  }, [organizationId, queryClient, refetchAfterReconnect]);

  const subscribeProject = useCallback((projectId: string) => {
    if (projectId.length === 0) return () => undefined;
    desiredProjectsRef.current.add(projectId);
    const connection = connectionRef.current;
    if (connection) void invokeSafely(connection, "SubscribeProject", projectId);

    return () => {
      desiredProjectsRef.current.delete(projectId);
      const currentConnection = connectionRef.current;
      if (currentConnection) void invokeSafely(currentConnection, "UnsubscribeProject", projectId);
    };
  }, []);

  const value = useMemo(() => ({ status, subscribeProject }), [status, subscribeProject]);
  return <RealtimeContext.Provider value={value}>{children}</RealtimeContext.Provider>;
}

import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { act, render, waitFor } from "@testing-library/react";
import { type PropsWithChildren } from "react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { RealtimeProvider } from "./RealtimeProvider";
import { useRealtimeProject } from "./useRealtimeProject";

const signalr = vi.hoisted(() => {
  const handlers = new Map<string, (message: unknown) => void>();
  const callbacks: {
    reconnecting?: () => void;
    reconnected?: () => Promise<void>;
    close?: () => void;
  } = {};
  const connection = {
    state: "Disconnected",
    start: vi.fn(async () => { connection.state = "Connected"; }),
    stop: vi.fn(async () => { connection.state = "Disconnected"; }),
    invoke: vi.fn(async () => undefined),
    on: vi.fn((name: string, handler: (message: unknown) => void) => handlers.set(name, handler)),
    onreconnecting: vi.fn((callback: () => void) => { callbacks.reconnecting = callback; }),
    onreconnected: vi.fn((callback: () => Promise<void>) => { callbacks.reconnected = callback; }),
    onclose: vi.fn((callback: () => void) => { callbacks.close = callback; }),
  };
  const builder = {
    withUrl: vi.fn(() => builder),
    withAutomaticReconnect: vi.fn(() => builder),
    configureLogging: vi.fn(() => builder),
    build: vi.fn(() => connection),
  };
  return { handlers, callbacks, connection, builder };
});

vi.mock("@microsoft/signalr", () => ({
  HubConnectionBuilder: class {
    constructor() {
      return signalr.builder;
    }
  },
  HubConnectionState: { Connected: "Connected" },
  LogLevel: { Warning: 3 },
}));

function ProjectSubscription() {
  useRealtimeProject("project-1");
  return null;
}

function TestProviders({ children }: PropsWithChildren) {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  return (
    <QueryClientProvider client={queryClient}>
      <RealtimeProvider organizationId="organization-1">{children}</RealtimeProvider>
    </QueryClientProvider>
  );
}

describe("RealtimeProvider", () => {
  beforeEach(() => {
    signalr.handlers.clear();
    signalr.connection.state = "Disconnected";
    signalr.connection.start.mockClear();
    signalr.connection.stop.mockClear();
    signalr.connection.invoke.mockClear();
    signalr.connection.on.mockClear();
    signalr.connection.onreconnecting.mockClear();
    signalr.connection.onreconnected.mockClear();
    signalr.connection.onclose.mockClear();
    signalr.builder.withUrl.mockClear();
    signalr.builder.withAutomaticReconnect.mockClear();
    signalr.builder.configureLogging.mockClear();
    signalr.builder.build.mockClear();
    delete signalr.callbacks.reconnecting;
    delete signalr.callbacks.reconnected;
    delete signalr.callbacks.close;
  });

  it("uses one credentialed connection and restores server-validated subscriptions", async () => {
    render(<ProjectSubscription />, { wrapper: TestProviders });

    await waitFor(() => expect(signalr.connection.start).toHaveBeenCalledOnce());
    expect(signalr.builder.withUrl).toHaveBeenCalledWith(
      "/hubs/realtime",
      { withCredentials: true },
    );
    await waitFor(() => {
      expect(signalr.connection.invoke).toHaveBeenCalledWith(
        "SubscribeOrganization",
        "organization-1",
      );
      expect(signalr.connection.invoke).toHaveBeenCalledWith("SubscribeProject", "project-1");
    });

    signalr.connection.invoke.mockClear();
    await act(async () => { await signalr.callbacks.reconnected?.(); });
    expect(signalr.connection.invoke).toHaveBeenCalledWith(
      "SubscribeOrganization",
      "organization-1",
    );
    expect(signalr.connection.invoke).toHaveBeenCalledWith("SubscribeProject", "project-1");
  });

  it("invalidates only event-related caches and ignores duplicate or cross-tenant events", async () => {
    const queryClient = new QueryClient({
      defaultOptions: { queries: { retry: false } },
    });
    const invalidate = vi.spyOn(queryClient, "invalidateQueries");
    render(
      <QueryClientProvider client={queryClient}>
        <RealtimeProvider organizationId="organization-1">
          <ProjectSubscription />
        </RealtimeProvider>
      </QueryClientProvider>,
    );
    await waitFor(() => expect(signalr.connection.start).toHaveBeenCalledOnce());

    act(() => signalr.handlers.get("WorkItemUpdated")?.({
      eventId: "event-1",
      organizationId: "organization-1",
      projectId: "project-1",
      workItemId: "work-item-1",
    }));
    expect(invalidate).toHaveBeenCalledWith({ queryKey: ["work-items", "project-1"] });
    expect(invalidate).toHaveBeenCalledWith({ queryKey: ["work-items", "detail", "work-item-1"] });
    expect(invalidate).toHaveBeenCalledWith({ queryKey: ["activity", "work-item-1"] });
    const firstEventCallCount = invalidate.mock.calls.length;

    act(() => {
      signalr.handlers.get("WorkItemUpdated")?.({
        eventId: "event-1",
        organizationId: "organization-1",
        projectId: "project-1",
        workItemId: "work-item-1",
      });
      signalr.handlers.get("CommentCreated")?.({
        eventId: "foreign-event",
        organizationId: "organization-2",
        projectId: "project-2",
        workItemId: "work-item-2",
      });
    });
    expect(invalidate).toHaveBeenCalledTimes(firstEventCallCount);

    act(() => signalr.handlers.get("CommentCreated")?.({
      eventId: "event-2",
      organizationId: "organization-1",
      projectId: "project-1",
      workItemId: "work-item-1",
      commentId: "comment-1",
    }));
    expect(invalidate).toHaveBeenCalledWith({ queryKey: ["comments", "work-item-1"] });

    act(() => signalr.handlers.get("NotificationCreated")?.({
      eventId: "event-3",
      notificationId: "notification-1",
    }));
    expect(invalidate).toHaveBeenCalledWith({ queryKey: ["notifications"] });
  });
});

import { describe, expect, it, vi, beforeEach } from "vitest";
import { render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { ChatEmptyState } from "@/components/chat/ChatEmptyState";
import { ChatMessages } from "@/components/chat/ChatMessages";
import { StatusCard } from "@/components/chat/StatusCard";
import type { ChatMessage } from "@/lib/chat-types";

describe("ChatEmptyState", () => {
  it("renders setup empty state when no session", () => {
    render(<ChatEmptyState hasSession={false} onCreateSession={() => {}} />);
    expect(
      screen.getByText(/Open or create a workspace to start chatting with Hermes/i),
    ).toBeInTheDocument();
  });
});

describe("ChatMessages", () => {
  it("renders streaming assistant message", () => {
    const messages: ChatMessage[] = [
      { id: "1", role: "assistant", content: "Hello", streaming: true },
    ];
    render(<ChatMessages messages={messages} />);
    expect(screen.getByText("typing…")).toBeInTheDocument();
    expect(screen.getByText("Hello")).toBeInTheDocument();
  });

  it("renders reconnect/error retry affordance", () => {
    const messages: ChatMessage[] = [
      {
        id: "e1",
        role: "error",
        content: "Stream failed",
        retryable: true,
      },
    ];
    const onRetry = vi.fn();
    render(<ChatMessages messages={messages} onRetry={onRetry} />);
    expect(screen.getByText("Stream failed")).toBeInTheDocument();
  });
});

describe("StatusCard", () => {
  it("renders autopilot accepted status card", () => {
    render(
      <StatusCard
        kind="autopilot_accepted"
        title="Fix login bug"
        content='Autopilot accepted "Fix login bug" without manual review.'
      />,
    );
    expect(screen.getByText("Autopilot accepted")).toBeInTheDocument();
  });

  it("renders needs-review protected-path block card", () => {
    render(
      <StatusCard
        kind="protected_path_block"
        title="Auth refactor"
        content="Blocked: touched protected path src/auth/login.ts"
        actions={[{ id: "accept", label: "Accept result" }]}
        onAction={vi.fn()}
      />,
    );
    expect(screen.getByText("Protected path block")).toBeInTheDocument();
    expect(
      screen.getByText("Blocked: touched protected path src/auth/login.ts"),
    ).toBeInTheDocument();
  });

  it("renders overlap block card", () => {
    render(
      <StatusCard
        kind="overlap_block"
        title="Worker B"
        content="Blocked: overlapping worker diff (src/a.ts)"
      />,
    );
    expect(screen.getByText("Overlap block")).toBeInTheDocument();
  });
});

describe("chat sidebar session labels", () => {
  it("shows session + counts via sidebar mock", async () => {
    const { ChatSidebar } = await import("@/components/chat/ChatSidebar");
    render(
      <ChatSidebar
        collapsed={false}
        onToggleCollapse={() => {}}
        sessions={[
          {
            id: "s1",
            name: "Demo",
            workspaceRoot: "/tmp/demo",
            hermesProfile: null,
          },
        ]}
        sessionId="s1"
        onSessionChange={() => {}}
        onNewChat={() => {}}
        recentThreads={[]}
        onLoadThread={() => {}}
        summary={{
          activeTaskCount: 2,
          blockedCount: 1,
          needsReviewCount: 1,
          autopilotProfile: "Balanced-Auto",
          autopilotEnabled: true,
        }}
        cpHealth="ok"
      />,
    );
    const status = screen.getByText("Session status").closest("div");
    expect(status).toBeTruthy();
    expect(within(status!).getByText("2")).toBeInTheDocument();
    expect(screen.getByText("Active tasks")).toBeInTheDocument();
    expect(screen.getByText("Demo")).toBeInTheDocument();
  });
});

describe("send message flow (mocked hook surface)", () => {
  it("disables duplicate sends while inflight via composer", async () => {
    const user = userEvent.setup();
    const { ChatComposer } = await import("@/components/chat/ChatComposer");
    const onSend = vi.fn();
    render(
      <ChatComposer
        value="hello"
        onChange={() => {}}
        onSend={onSend}
        onChip={() => {}}
        streaming
        sendInflight
      />,
    );
    const btn = screen.getByRole("button", { name: "Send message" });
    expect(btn).toBeDisabled();
    await user.click(btn);
    expect(onSend).not.toHaveBeenCalled();
  });
});

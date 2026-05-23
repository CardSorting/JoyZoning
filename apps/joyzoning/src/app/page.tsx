"use client";

import { Suspense } from "react";
import { ChatShell } from "@/components/chat/ChatShell";

function ChatLoadingFallback() {
  return (
    <div className="flex h-screen items-center justify-center bg-gpt-main text-gpt-muted">
      <p>Loading chat…</p>
    </div>
  );
}

export default function HomePage() {
  return (
    <Suspense fallback={<ChatLoadingFallback />}>
      <ChatShell />
    </Suspense>
  );
}

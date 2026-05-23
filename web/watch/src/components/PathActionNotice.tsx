"use client";

export function PathActionNotice({
  message,
  variant = "info",
}: {
  message: string | null;
  variant?: "info" | "error";
}) {
  if (!message) return null;
  return (
    <p
      role="status"
      className={`text-[11px] ${
        variant === "error" ? "text-red-300" : "text-emerald-300/90"
      }`}
    >
      {message}
    </p>
  );
}

/**
 * API base URL. Empty string = same origin (control plane serves the static export on :9470).
 * In `next dev`, rewrites proxy /api and /hubs to the control plane.
 */
export const API_BASE =
  typeof process !== "undefined" && process.env.NEXT_PUBLIC_API_BASE
    ? process.env.NEXT_PUBLIC_API_BASE.replace(/\/$/, "")
    : "";

export function apiUrl(path: string): string {
  const p = path.startsWith("/") ? path : `/${path}`;
  return `${API_BASE}${p}`;
}

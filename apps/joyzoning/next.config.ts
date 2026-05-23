import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  output: "export",
  trailingSlash: true,
  images: { unoptimized: true },
  /** Dev-only: proxy API + SignalR to control plane (production is same-origin from :9470). */
  async rewrites() {
    if (process.env.NODE_ENV !== "development") return [];
    const cp = process.env.CONTROL_PLANE_URL ?? "http://127.0.0.1:9470";
    return [
      { source: "/api/:path*", destination: `${cp}/api/:path*` },
      { source: "/hubs/:path*", destination: `${cp}/hubs/:path*` },
    ];
  },
};

export default nextConfig;

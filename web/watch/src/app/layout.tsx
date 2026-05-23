import type { Metadata } from "next";
import "./globals.css";

export const metadata: Metadata = {
  title: "JoyZone Watch · Synthesis Pet",
  description:
    "Tamagotchi for agent orchestration — one synthesis pet mirrors run health; stack traces live in the observatory basement.",
};

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  return (
    <html lang="en">
      <body className="min-h-screen overflow-x-hidden">{children}</body>
    </html>
  );
}

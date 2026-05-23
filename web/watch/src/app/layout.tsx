import type { Metadata } from "next";
import "./globals.css";

export const metadata: Metadata = {
  title: "JoyZoning Operator Watch",
  description:
    "Single-screen operator console for task execution, merge review, and workspace actions.",
};

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  return (
    <html lang="en">
      <body className="min-h-screen overflow-x-hidden bg-zinc-950 text-zinc-100">
        {children}
      </body>
    </html>
  );
}

import type { Metadata } from "next";
import "./globals.css";

export const metadata: Metadata = {
  title: "JoyZoning",
  description: "Chat with Hermes — your JoyZoning project lead.",
};

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  return (
    <html lang="en" className="h-full">
      <body className="h-full overflow-hidden bg-gpt-main text-gpt-text antialiased">
        {children}
      </body>
    </html>
  );
}

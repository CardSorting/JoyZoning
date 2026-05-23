import type { Config } from "tailwindcss";

const config: Config = {
  content: ["./src/**/*.{js,ts,jsx,tsx}"],
  theme: {
    extend: {
      colors: {
        pet: {
          night: "#12101c",
          deep: "#1c1828",
          shell: "#2a2640",
          elevated: "#38324f",
          cream: "#faf6ef",
          mint: "#9ed4b5",
          sky: "#a8d4f0",
          lavender: "#c4b5e8",
          rose: "#e8a0a8",
          gold: "#f0d78c",
          muted: "#a89fc4",
        },
        cozy: {
          night: "#1a1628",
          deep: "#221e35",
          surface: "#2d2842",
          elevated: "#3a3550",
          cream: "#faf6ef",
          peach: "#f4c9a8",
          sage: "#9ed4b5",
          sky: "#a8d4f0",
          lavender: "#c4b5e8",
          rose: "#e8b4b8",
          gold: "#f0d78c",
          text: "#f5f0e8",
          muted: "#b8aed0",
        },
        jz: {
          void: "#12101c",
          deep: "#1c1828",
          surface: "#2a2640",
          elevated: "#38324f",
          cyan: "#a8d4f0",
          violet: "#c4b5e8",
          rose: "#e8a0a8",
          gold: "#f0d78c",
          mint: "#9ed4b5",
          text: "#f5f0e8",
          muted: "#a89fc4",
        },
      },
      borderRadius: {
        pet: "1.25rem",
        "pet-lg": "1.75rem",
        cozy: "1.25rem",
        "cozy-lg": "1.75rem",
      },
      boxShadow: {
        pet: "0 4px 18px rgba(158, 212, 181, 0.12), 0 8px 28px rgba(0,0,0,0.22)",
        cozy: "0 4px 20px rgba(158, 212, 181, 0.15), 0 8px 32px rgba(0,0,0,0.2)",
      },
      animation: {
        "pet-bob": "pet-bob 3.2s ease-in-out infinite",
      },
      keyframes: {
        "pet-bob": {
          "0%, 100%": { transform: "translateY(0)" },
          "50%": { transform: "translateY(-6px)" },
        },
      },
    },
  },
  plugins: [],
};

export default config;

import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";
import tailwindcss from "@tailwindcss/vite";

// API_URL lets a second API instance run side by side (e.g. API_URL=http://localhost:4243 npm run dev).
const apiUrl = process.env.API_URL ?? "http://localhost:4242";

export default defineConfig({
  plugins: [react(), tailwindcss()],
  server: { proxy: { "/api": apiUrl } },
});

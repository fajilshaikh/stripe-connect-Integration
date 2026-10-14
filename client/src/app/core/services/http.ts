// Single place for API calls and error handling (the React equivalent of skinet's error interceptor).
const baseUrl = "/api"; // proxied to the .NET API by Vite (vite.config.ts)

/** API error bodies: ApiErrorResponse from ExceptionMiddleware, or ProblemDetails from model validation. */
type ApiError = {
  message?: string;
  title?: string;
  errors?: Record<string, string[]>;
};

const errorMessage = (data: ApiError | null, fallback: string) =>
  data?.message ?? Object.values(data?.errors ?? {})[0]?.[0] ?? data?.title ?? fallback;

async function request<T>(method: "GET" | "POST", path: string, body?: unknown): Promise<T> {
  const res = await fetch(`${baseUrl}${path}`, {
    method,
    headers: { "Content-Type": "application/json" },
    body: body === undefined ? undefined : JSON.stringify(body),
  });
  const data = await res.json().catch(() => null);
  if (!res.ok) throw new Error(errorMessage(data, res.statusText));
  return data as T;
}

export const http = {
  get: <T>(path: string) => request<T>("GET", path),
  post: <T>(path: string, body: unknown = {}) => request<T>("POST", path, body),
};

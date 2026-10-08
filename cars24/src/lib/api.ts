// Shared API configuration for the Cars24 backend API wrappers.
//
// The base URL is controlled by the NEXT_PUBLIC_API_URL environment
// variable (see .env.example):
//   - local development: http://localhost:5203
//   - production: set NEXT_PUBLIC_API_URL to the deployed backend URL
//     in the hosting provider's environment settings.
export const API_BASE_URL =
  process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5203";

// Shared response handler for API wrappers. It surfaces useful errors for
// non-2xx responses instead of failing on a JSON parse or silently returning
// a parsed error body.
export async function parseResponse<T = any>(
  response: Response,
  fallbackMessage: string
): Promise<T> {
  if (!response.ok) {
    let detail = "";
    try {
      const text = await response.text();
      if (text) {
        try {
          const json = JSON.parse(text);
          detail =
            json?.message ??
            json?.error ??
            (typeof json === "string" ? json : text);
        } catch {
          detail = text;
        }
      }
    } catch {
      // Ignore body read failures; fall back to the status text.
    }
    throw new Error(
      `${fallbackMessage} (HTTP ${response.status})${detail ? `: ${detail}` : ""}`
    );
  }
  return (await response.json()) as T;
}

// Shared API configuration for the Cars24 backend API wrappers.
//
// The base URL is controlled by the NEXT_PUBLIC_API_URL environment
// variable (see .env.example):
//   - local development: http://localhost:5203
//   - production: set NEXT_PUBLIC_API_URL to the deployed backend URL
//     in the hosting provider's environment settings.
export const API_BASE_URL =
  process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5203";

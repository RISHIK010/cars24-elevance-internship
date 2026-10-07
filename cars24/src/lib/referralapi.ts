import { API_BASE_URL } from "@/lib/api";

const BASE_URL = `${API_BASE_URL}/api/Referral`;

export const getReferralSummary = async (userId: string) => {
  const response = await fetch(`${BASE_URL}/summary/${userId}`);
  if (!response.ok) {
    throw new Error("Failed to fetch referral summary");
  }
  return response.json();
};

export const attachReferral = async (userId: string, referralCode: string) => {
  const response = await fetch(`${BASE_URL}/attach`, {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
    },
    body: JSON.stringify({ userId, referralCode }),
  });
  const data = await response.json().catch(() => null);
  if (!response.ok) {
    throw new Error((data as any)?.message ?? "Failed to attach referral");
  }
  return data;
};

import { API_BASE_URL } from "@/lib/api";

const BASE_URL = `${API_BASE_URL}/api/Wallet`;

export const getWallet = async (userId: string) => {
  const response = await fetch(`${BASE_URL}/${userId}`);
  if (!response.ok) {
    throw new Error("Failed to fetch wallet");
  }
  return response.json();
};

export const redeemWallet = async (
  userId: string,
  amount: number,
  idempotencyKey: string
) => {
  const response = await fetch(`${BASE_URL}/redeem`, {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
    },
    body: JSON.stringify({ userId, amount, idempotencyKey }),
  });
  const data = await response.json().catch(() => null);
  if (!response.ok) {
    throw new Error((data as any)?.message ?? "Failed to redeem");
  }
  return data;
};

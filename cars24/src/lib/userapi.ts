import { API_BASE_URL } from "@/lib/api";

const BASE_URL = `${API_BASE_URL}/api/UserAuth`;

export const signup = async (
  email: string,
  password: string,
  userData: { fullName: string; phone: string; referralCode?: string }
) => {
  const response = await fetch(`${BASE_URL}/signup`, {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
    },
    body: JSON.stringify({ email, password, ...userData }),
  });
  const data = await response.json().catch(() => null);
  if (!response.ok) {
    throw new Error((data as any)?.message ?? "Failed to sign up");
  }
  return data;
};

export const login = async (email: string, password: string) => {
  const response = await fetch(`${BASE_URL}/login`, {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
    },
    body: JSON.stringify({ email, password }),
  });
  if (!response.ok) {
    throw new Error("Failed to login");
  }
  return response.json();
};

export const getUserById = async (userId: string) => {
  const response = await fetch(`${BASE_URL}/${userId}`);
  if (!response.ok) {
    throw new Error("Failed to fetch user");
  }
  return response.json();
};

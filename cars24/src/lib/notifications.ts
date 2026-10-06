import { API_BASE_URL } from "@/lib/api";

const BASE_URL = `${API_BASE_URL}/api/Notification`;

export type NotificationPreferences = {
  browserPush: boolean;
  appointmentConfirmations: boolean;
  bidUpdates: boolean;
  priceDrops: boolean;
  newMessages: boolean;
};

export const defaultPreferences: NotificationPreferences = {
  browserPush: true,
  appointmentConfirmations: true,
  bidUpdates: true,
  priceDrops: true,
  newMessages: true,
};

export const registerDevice = async (payload: {
  userId: string;
  deviceId: string;
  registrationToken: string;
  preferences?: NotificationPreferences;
}) => {
  const response = await fetch(`${BASE_URL}/register`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(payload),
  });
  if (!response.ok) throw new Error("Failed to register notification device");
  return response.json();
};

export const unregisterDevice = async (userId: string, deviceId: string) => {
  const response = await fetch(
    `${BASE_URL}/${encodeURIComponent(userId)}/devices/${encodeURIComponent(deviceId)}`,
    { method: "DELETE" }
  );
  if (!response.ok && response.status !== 404)
    throw new Error("Failed to unregister notification device");
  return response.status;
};

export const getPreferences = async (
  userId: string
): Promise<NotificationPreferences | null> => {
  const response = await fetch(
    `${BASE_URL}/${encodeURIComponent(userId)}/preferences`
  );
  if (response.status === 404) return null;
  if (!response.ok) throw new Error("Failed to fetch notification preferences");
  return response.json();
};

export const updatePreferences = async (
  userId: string,
  preferences: NotificationPreferences
) => {
  const response = await fetch(
    `${BASE_URL}/${encodeURIComponent(userId)}/preferences`,
    {
      method: "PUT",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(preferences),
    }
  );
  if (!response.ok) throw new Error("Failed to update notification preferences");
  return response.json();
};

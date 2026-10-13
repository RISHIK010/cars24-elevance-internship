import { API_BASE_URL, parseResponse } from "@/lib/api";

const BASE_URL = `${API_BASE_URL}/api/Booking`;

export const createBooking = async (userid: string, Booking: any) => {
  const response = await fetch(`${BASE_URL}?userId=${userid}`, {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
    },
    body: JSON.stringify(Booking),
  });
  return parseResponse(response, "Failed to create booking");
};

export const completeBooking = async (userId: string, bookingId: string) => {
  const response = await fetch(
    `${BASE_URL}/${bookingId}/complete?userId=${userId}`,
    { method: "POST" }
  );
  const data = await response.json().catch(() => null);
  if (!response.ok) {
    throw new Error((data as any)?.message ?? "Failed to complete booking");
  }
  return data;
};

export const getBookingbyid = async (id: string) => {
  const response = await fetch(`${BASE_URL}/${id}`);
  return response.json();
};
export const getBookingbyuser = async (userId: string) => {
  const response = await fetch(`${BASE_URL}/user/${userId}/bookings`);
  return parseResponse(response, "Failed to fetch bookings");
};

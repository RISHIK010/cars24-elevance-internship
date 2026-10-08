import { API_BASE_URL, parseResponse } from "@/lib/api";

const BASE_URL = `${API_BASE_URL}/api/Appointment`;

export const createAppointment = async (userid: string, appointment: any) => {
  const response = await fetch(`${BASE_URL}?userId=${userid}`, {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
    },
    body: JSON.stringify(appointment),
  });
  return parseResponse(response, "Failed to create appointment");
};

export const getAppointmentbyid = async (id: string) => {
  const response = await fetch(`${BASE_URL}/${id}`);
  return response.json();
};
export const getappointmentbyuser = async (userId:string) => {
  const response = await fetch(`${BASE_URL}/user/${userId}/appointments`);
  return parseResponse(response, "Failed to fetch appointments");
};
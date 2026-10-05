import { API_BASE_URL } from "@/lib/api";

const BASE_URL = `${API_BASE_URL}/api/Car`;

type CarDetails = {
  title: string;
  images: string[];
  price: string;
  emi: string;
  location: string;
  specs: {
    year: number;
    km: string;
    fuel: string;
    transmission: string;
    owner: string;
    insurance: string;
  };
  features: string[];
  highlights: string[];
};
export const createCar = async (carDetails: CarDetails) => {
  const response = await fetch(`${BASE_URL}`, {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
    },
    body: JSON.stringify(carDetails),
  });
  return response.json();
};
export const getcarByid = async (id: string) => {
  const response = await fetch(`${BASE_URL}/${id}`);
  return response.json();
};
export const getcarSummaries = async (city?: string) => {
  const url = city
    ? `${BASE_URL}/summaries?city=${encodeURIComponent(city)}`
    : `${BASE_URL}/summaries`;
  const response = await fetch(url);
  return response.json();
};

export type SupportedCity = {
  name: string;
  latitude: number;
  longitude: number;
};

export type ServiceHub = {
  name: string;
  type: string; // ServiceCenter | PickupPoint | Hub
  city: string;
  address: string;
  latitude: number;
  longitude: number;
};

export const getSupportedCities = async (): Promise<SupportedCity[]> => {
  const response = await fetch(`${BASE_URL}/cities`);
  return response.json();
};

export const getHubs = async (city: string): Promise<ServiceHub[]> => {
  const response = await fetch(
    `${BASE_URL}/hubs?city=${encodeURIComponent(city)}`
  );
  return response.json();
};

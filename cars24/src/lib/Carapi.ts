import { API_BASE_URL, parseResponse } from "@/lib/api";

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
  return parseResponse(response, "Failed to fetch car");
};

export type CarSearchParams = {
  search?: string;
  city?: string;
  brand?: string | string[];
  fuel?: string;
  transmission?: string;
  minKm?: number | string;
  maxKm?: number | string;
  minYear?: number | string;
  maxYear?: number | string;
  minPrice?: number | string;
  maxPrice?: number | string;
  sort?: string;
};

export const getcarSummaries = async (params: CarSearchParams = {}) => {
  const query = new URLSearchParams();
  (Object.keys(params) as (keyof CarSearchParams)[]).forEach((key) => {
    const value = params[key];
    if (value === undefined || value === null || value === "") return;
    if (Array.isArray(value)) {
      value.forEach((v) => {
        if (v !== "") query.append(key, v);
      });
    } else {
      query.set(key, String(value));
    }
  });
  const qs = query.toString();
  const url = qs ? `${BASE_URL}/summaries?${qs}` : `${BASE_URL}/summaries`;
  const response = await fetch(url);
  return parseResponse(response, "Failed to fetch car summaries");
};

export type CarSuggestion = {
  value: string;
  category: string;
};

export const getSearchSuggestions = async (): Promise<CarSuggestion[]> => {
  const response = await fetch(`${BASE_URL}/suggestions`);
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

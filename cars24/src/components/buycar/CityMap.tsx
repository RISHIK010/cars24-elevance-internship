import React from "react";
import dynamic from "next/dynamic";
import type { ServiceHub, SupportedCity } from "@/lib/Carapi";

// Leaflet relies on browser globals, so the map must never be rendered
// on the server. Load it client-side only.
const MapView = dynamic(() => import("./MapView"), {
  ssr: false,
  loading: () => (
    <div className="h-full w-full animate-pulse bg-gray-200 rounded-lg" />
  ),
});

interface CityMapProps {
  city: SupportedCity;
  hubs: ServiceHub[];
}

const CityMap: React.FC<CityMapProps> = ({ city, hubs }) => {
  return (
    <div className="bg-white p-4 rounded-lg shadow mb-6">
      <h3 className="font-semibold mb-1">
        Service centers &amp; pickup hubs in {city.name}
      </h3>
      <p className="text-sm text-gray-600 mb-3">
        Visit a CARS24 hub near you for inspection, pickup and support.
      </p>
      <div className="h-72 w-full rounded-lg overflow-hidden relative z-0">
        <MapView city={city} hubs={hubs} />
      </div>
    </div>
  );
};

export default CityMap;

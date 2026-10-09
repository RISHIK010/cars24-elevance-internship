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
  const hasHubs = hubs.length > 0;

  return (
    <div className="bg-white p-4 rounded-lg shadow mb-6">
      <h3 className="font-semibold mb-1">
        Service centers &amp; pickup hubs in {city.name}
      </h3>
      <p className="text-sm text-gray-600 mb-3">
        Visit a CARS24 hub near you for inspection, pickup and support.
      </p>
      <div className="relative h-72 w-full rounded-lg overflow-hidden z-0">
        <MapView city={city} hubs={hubs} />
        {!hasHubs && (
          <div className="absolute inset-0 flex items-center justify-center bg-white/80 pointer-events-none">
            <div className="text-center px-4 py-3 max-w-sm">
              <p className="font-medium text-gray-800">
                Verified CARS24 hubs are unavailable in {city.name} right now.
              </p>
              <p className="text-sm text-gray-500 mt-1">
                The map is centered on {city.name}, but no service centers or
                pickup points have been verified here yet.
              </p>
            </div>
          </div>
        )}
      </div>
    </div>
  );
};

export default CityMap;

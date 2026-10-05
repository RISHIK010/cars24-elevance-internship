import { Button } from "@/components/ui/button";
import { Loader2, LocateFixed, MapPin, X } from "lucide-react";
import React from "react";
import type { SupportedCity } from "@/lib/Carapi";

interface CitySelectorProps {
  cities: SupportedCity[];
  selectedCity: string;
  locating: boolean;
  onSelectCity: (city: string) => void;
  onUseMyLocation: () => void;
  onClearCity: () => void;
}

// Manual city selector + "Use my location" action for the buy-car page.
// City selection is the geo-fence applied to the car listings.
const CitySelector: React.FC<CitySelectorProps> = ({
  cities,
  selectedCity,
  locating,
  onSelectCity,
  onUseMyLocation,
  onClearCity,
}) => {
  return (
    <div className="bg-white p-4 rounded-lg shadow mb-6">
      <div className="flex flex-col sm:flex-row sm:items-center gap-3">
        <div className="flex items-center text-sm font-medium text-gray-700">
          <MapPin className="h-4 w-4 mr-1 text-blue-600" />
          Your city
        </div>
        <div className="flex flex-1 flex-col sm:flex-row gap-3">
          <select
            aria-label="Select city"
            className="flex-1 border border-gray-300 rounded-md px-3 py-2 text-sm text-gray-900 bg-white focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500"
            value={selectedCity}
            onChange={(e) => onSelectCity(e.target.value)}
          >
            <option value="">All cities</option>
            {cities.map((city) => (
              <option key={city.name} value={city.name}>
                {city.name}
              </option>
            ))}
          </select>
          <Button
            type="button"
            variant="outline"
            onClick={onUseMyLocation}
            disabled={locating}
            className="flex items-center justify-center text-gray-700"
          >
            {locating ? (
              <Loader2 className="h-4 w-4 mr-2 animate-spin" />
            ) : (
              <LocateFixed className="h-4 w-4 mr-2" />
            )}
            {locating ? "Detecting..." : "Use my location"}
          </Button>
          {selectedCity && (
            <Button
              type="button"
              variant="ghost"
              onClick={onClearCity}
              className="flex items-center justify-center text-gray-500"
            >
              <X className="h-4 w-4 mr-1" />
              Clear
            </Button>
          )}
        </div>
      </div>
      {selectedCity && (
        <p className="mt-3 text-sm text-gray-600">
          Showing cars available in{" "}
          <span className="font-semibold text-gray-900">{selectedCity}</span>{" "}
          only.
        </p>
      )}
    </div>
  );
};

export default CitySelector;

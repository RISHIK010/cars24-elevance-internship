"use client";
import CityMap from "@/components/buycar/CityMap";
import CitySelector from "@/components/buycar/CitySelector";
import SearchAutocomplete from "@/components/search/SearchAutocomplete";
import { Slider } from "@/components/ui/slider";
import {
  getcarSummaries,
  getHubs,
  getSupportedCities,
  ServiceHub,
  SupportedCity,
} from "@/lib/Carapi";
import { findNearestCity } from "@/lib/geo";
import { CAR_IMAGE_PLACEHOLDER } from "@/lib/carImagePlaceholder";
import { ChevronDown, Heart, Search, Sliders } from "lucide-react";
import Link from "next/link";
import { useRouter } from "next/router";
import React, { useEffect, useState } from "react";
import { toast } from "sonner";

const BRANDS = ["Maruti", "Hyundai", "Honda", "Tata"];
const FUEL_TYPES = ["Petrol", "Diesel", "CNG", "Electric", "Hybrid", "LPG"];
const TRANSMISSIONS = ["Manual", "Automatic", "AMT", "CVT", "DCT"];
const SORT_OPTIONS = [
  { value: "relevance", label: "Relevance" },
  { value: "price_asc", label: "Price: Low to High" },
  { value: "price_desc", label: "Price: High to Low" },
  { value: "km_asc", label: "Km: Low to High" },
  { value: "year_desc", label: "Year: Newest First" },
];
const MAX_PRICE = 1000000;

interface Car {
  id: string;
  title: string;
  km: string;
  fuel: string;
  transmission: string;
  owner: string;
  year: number;
  emi: string;
  price: string;
  recommendedPrice: number | null;
  location: string;
  image: string;
  estimatedMonthlyMaintenance: number | null;
  maintenanceStatus: string | null;
  maintenanceInsights: string[];
}

function LoaderCard() {
  return (
    <div className="bg-white rounded-lg shadow-md animate-pulse overflow-hidden">
      <div className="h-48 bg-gray-200"></div>
      <div className="p-4 space-y-2">
        <div className="h-4 bg-gray-200 rounded w-3/4"></div>
        <div className="h-3 bg-gray-200 rounded w-full"></div>
        <div className="h-3 bg-gray-200 rounded w-1/2"></div>
        <div className="h-3 bg-gray-200 rounded w-2/3"></div>
      </div>
    </div>
  );
}

const index = () => {
  const router = useRouter();
  const [cars, setCars] = useState<Car[] | null>(null);
  const [supportedCities, setSupportedCities] = useState<SupportedCity[]>([]);
  const [hubs, setHubs] = useState<ServiceHub[]>([]);
  const [locating, setLocating] = useState(false);
  const [priceRange, setPriceRange] = useState<[number, number]>([0, MAX_PRICE]);
  const [searchText, setSearchText] = useState("");

  const qstr = (key: string): string =>
    typeof router.query[key] === "string" ? (router.query[key] as string) : "";

  const brands: string[] = Array.isArray(router.query.brand)
    ? (router.query.brand as string[])
    : router.query.brand
    ? [router.query.brand as string]
    : [];

  const city = qstr("city");
  const fuel = qstr("fuel");
  const transmission = qstr("transmission");
  const sort = qstr("sort");
  const minYear = qstr("minYear");
  const maxYear = qstr("maxYear");
  const minKm = qstr("minKm");
  const maxKm = qstr("maxKm");

  // Update only the provided query params so unrelated filters survive.
  const updateQuery = (
    next: Record<string, string | string[] | undefined>
  ) => {
    const query: Record<string, string | string[]> = {
      ...(router.query as Record<string, string | string[]>),
    };
    for (const [key, value] of Object.entries(next)) {
      if (
        value === undefined ||
        value === null ||
        value === "" ||
        (Array.isArray(value) && value.length === 0)
      ) {
        delete query[key];
      } else {
        query[key] = value;
      }
    }
    router.push({ pathname: router.pathname, query }, undefined, {
      shallow: true,
    });
  };
  // Sync the search box only when the ?search= param changes (e.g. refresh,
  // Hero navigation or shared link) so in-progress typing is not clobbered.
  useEffect(() => {
    if (!router.isReady) return;
    setSearchText(qstr("search"));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [router.isReady, router.query.search]);

  // Sync the price slider only when the price params change.
  useEffect(() => {
    if (!router.isReady) return;
    const min = qstr("minPrice");
    const max = qstr("maxPrice");
    setPriceRange([min ? Number(min) : 0, max ? Number(max) : MAX_PRICE]);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [router.isReady, router.query.minPrice, router.query.maxPrice]);

  useEffect(() => {
    const loadCities = async () => {
      try {
        const cities = await getSupportedCities();
        setSupportedCities(cities);
      } catch {
        toast.error("Unable to load supported cities right now.");
      }
    };
    loadCities();
  }, []);

  // Fetch listings whenever any search/filter param changes.
  useEffect(() => {
    if (!router.isReady) return;
    const brandParam = Array.isArray(router.query.brand)
      ? (router.query.brand as string[])
      : router.query.brand
      ? [router.query.brand as string]
      : undefined;

    const fetchCars = async () => {
      try {
        const data = await getcarSummaries({
          search: qstr("search") || undefined,
          city: qstr("city") || undefined,
          brand: brandParam,
          fuel: qstr("fuel") || undefined,
          transmission: qstr("transmission") || undefined,
          minKm: qstr("minKm") || undefined,
          maxKm: qstr("maxKm") || undefined,
          minYear: qstr("minYear") || undefined,
          maxYear: qstr("maxYear") || undefined,
          minPrice: qstr("minPrice") || undefined,
          maxPrice: qstr("maxPrice") || undefined,
          sort: qstr("sort") || undefined,
        });
        setCars(Array.isArray(data) ? data : []);
      } catch {
        setCars([]);
        toast.error("Unable to load cars right now.");
      }
    };
    fetchCars();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [router.isReady, router.query]);

  // Load the service centers / pickup hubs for the map.
  useEffect(() => {
    if (!city) {
      setHubs([]);
      return;
    }
    const loadHubs = async () => {
      try {
        setHubs(await getHubs(city));
      } catch {
        setHubs([]);
      }
    };
    loadHubs();
  }, [city]);

  const handleSearch = (value: string) => {
    const trimmed = value.trim();
    setSearchText(trimmed);
    updateQuery({ search: trimmed || undefined });
  };

  const toggleBrand = (brand: string) => {
    const next = new Set(brands);
    if (next.has(brand)) next.delete(brand);
    else next.add(brand);
    updateQuery({ brand: next.size ? Array.from(next) : undefined });
  };

  const handlePriceCommit = (value: number[]) => {
    updateQuery({
      minPrice: value[0] > 0 ? String(value[0]) : undefined,
      maxPrice: value[1] < MAX_PRICE ? String(value[1]) : undefined,
    });
  };
  const handleUseMyLocation = () => {
    if (typeof navigator === "undefined" || !("geolocation" in navigator)) {
      toast.error(
        "Location services are not supported by this browser. Please select your city manually."
      );
      return;
    }
    if (supportedCities.length === 0) {
      toast.error(
        "Supported cities are still loading. Please try again or select your city manually."
      );
      return;
    }
    setLocating(true);
    // One-shot, user-triggered position request. No tracking or watching.
    navigator.geolocation.getCurrentPosition(
      (position) => {
        setLocating(false);
        const nearest = findNearestCity(
          {
            latitude: position.coords.latitude,
            longitude: position.coords.longitude,
          },
          supportedCities
        );
        if (nearest) {
          updateQuery({ city: nearest.name });
          toast.success(`Location detected. Showing cars in ${nearest.name}.`);
        } else {
          toast.error(
            "Could not match your location to a supported city. Please select it manually."
          );
        }
      },
      (error) => {
        setLocating(false);
        if (error.code === error.PERMISSION_DENIED) {
          toast.error(
            "Location permission denied. Please select your city manually."
          );
        } else if (error.code === error.TIMEOUT) {
          toast.error(
            "Location request timed out. Please try again or select your city manually."
          );
        } else {
          toast.error(
            "Unable to detect your location. Please select your city manually."
          );
        }
      },
      { enableHighAccuracy: false, timeout: 10000, maximumAge: 300000 }
    );
  };

  const selectedCityInfo = supportedCities.find((c) => c.name === city);

  return (
    <div className="bg-gray-100">
      <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8 bg-white text-black">
        <div className="grid grid-cols-1 md:grid-cols-4 gap-6">
          {/* filter */}
          <div className="md:col-span-1 space-y-6">
            <div className="bg-white p-4 rounded-lg shadow">
              <h3 className="font-semibold mb-4">Filters</h3>
              <div className="space-y-5">
                <div>
                  <label className="text-sm font-medium mb-2 block">
                    Price Range
                  </label>
                  <Slider
                    min={0}
                    max={MAX_PRICE}
                    step={10000}
                    value={priceRange}
                    onValueChange={(v) => setPriceRange([v[0], v[1]])}
                    onValueCommit={handlePriceCommit}
                    className="mt-2"
                  />
                  <div className="flex justify-between mt-2 text-sm text-gray-600">
                    <span>₹{priceRange[0].toLocaleString("en-IN")}</span>
                    <span>₹{priceRange[1].toLocaleString("en-IN")}</span>
                  </div>
                </div>

                <div>
                  <label className="text-sm font-medium mb-2 block">Brand</label>
                  <div className="space-y-2">
                    {BRANDS.map((brand) => (
                      <label key={brand} className="flex items-center">
                        <input
                          type="checkbox"
                          className="rounded border-gray-300 text-blue-600 focus:ring-blue-500"
                          checked={brands.includes(brand)}
                          onChange={() => toggleBrand(brand)}
                        />
                        <span className="ml-2 text-sm">{brand}</span>
                      </label>
                    ))}
                  </div>
                </div>
                <div>
                  <label className="text-sm font-medium mb-2 block">
                    Fuel Type
                  </label>
                  <select
                    aria-label="Fuel type"
                    value={fuel}
                    onChange={(e) =>
                      updateQuery({ fuel: e.target.value || undefined })
                    }
                    className="w-full border border-gray-300 rounded-md px-3 py-2 text-sm bg-white focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500"
                  >
                    <option value="">Any</option>
                    {FUEL_TYPES.map((f) => (
                      <option key={f} value={f}>
                        {f}
                      </option>
                    ))}
                  </select>
                </div>

                <div>
                  <label className="text-sm font-medium mb-2 block">
                    Transmission
                  </label>
                  <select
                    aria-label="Transmission"
                    value={transmission}
                    onChange={(e) =>
                      updateQuery({ transmission: e.target.value || undefined })
                    }
                    className="w-full border border-gray-300 rounded-md px-3 py-2 text-sm bg-white focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500"
                  >
                    <option value="">Any</option>
                    {TRANSMISSIONS.map((t) => (
                      <option key={t} value={t}>
                        {t}
                      </option>
                    ))}
                  </select>
                </div>

                <div>
                  <label className="text-sm font-medium mb-2 block">
                    Year of Manufacture
                  </label>
                  <div className="flex items-center gap-2">
                    <input
                      type="number"
                      min={1990}
                      max={new Date().getFullYear()}
                      placeholder="Min"
                      aria-label="Minimum year"
                      value={minYear}
                      onChange={(e) =>
                        updateQuery({ minYear: e.target.value || undefined })
                      }
                      className="w-full border border-gray-300 rounded-md px-3 py-2 text-sm"
                    />
                    <span className="text-gray-400">-</span>
                    <input
                      type="number"
                      min={1990}
                      max={new Date().getFullYear()}
                      placeholder="Max"
                      aria-label="Maximum year"
                      value={maxYear}
                      onChange={(e) =>
                        updateQuery({ maxYear: e.target.value || undefined })
                      }
                      className="w-full border border-gray-300 rounded-md px-3 py-2 text-sm"
                    />
                  </div>
                </div>

                <div>
                  <label className="text-sm font-medium mb-2 block">
                    Mileage (km)
                  </label>
                  <div className="flex items-center gap-2">
                    <input
                      type="number"
                      min={0}
                      placeholder="Min km"
                      aria-label="Minimum mileage"
                      value={minKm}
                      onChange={(e) =>
                        updateQuery({ minKm: e.target.value || undefined })
                      }
                      className="w-full border border-gray-300 rounded-md px-3 py-2 text-sm"
                    />
                    <span className="text-gray-400">-</span>
                    <input
                      type="number"
                      min={0}
                      placeholder="Max km"
                      aria-label="Maximum mileage"
                      value={maxKm}
                      onChange={(e) =>
                        updateQuery({ maxKm: e.target.value || undefined })
                      }
                      className="w-full border border-gray-300 rounded-md px-3 py-2 text-sm"
                    />
                  </div>
                </div>
              </div>
            </div>
          </div>
          {/* cars grid */}
          <div className="md:col-span-3">
            <CitySelector
              cities={supportedCities}
              selectedCity={city}
              locating={locating}
              onSelectCity={(c) => updateQuery({ city: c || undefined })}
              onUseMyLocation={handleUseMyLocation}
              onClearCity={() => updateQuery({ city: undefined })}
            />

            {selectedCityInfo && <CityMap city={selectedCityInfo} hubs={hubs} />}

            <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3 mb-6">
              <h1 className="text-2xl font-bold">
                Used Cars {city ? `in ${city}` : "Across India"}
              </h1>
              <div className="flex flex-col sm:flex-row items-stretch sm:items-center gap-3">
                <div className="relative w-full sm:w-64">
                  <Search className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-gray-400" />
                  <SearchAutocomplete
                    value={searchText}
                    onChange={setSearchText}
                    onSubmit={handleSearch}
                    placeholder="Search cars..."
                    className="w-full"
                    inputClassName="pl-10"
                  />
                </div>
                <div className="relative">
                  <Sliders className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-gray-400 pointer-events-none" />
                  <select
                    aria-label="Sort"
                    value={sort}
                    onChange={(e) =>
                      updateQuery({ sort: e.target.value || undefined })
                    }
                    className="pl-9 pr-8 py-2 border border-gray-300 rounded-md text-sm bg-white focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500"
                  >
                    {SORT_OPTIONS.map((opt) => (
                      <option key={opt.value} value={opt.value}>
                        {opt.label}
                      </option>
                    ))}
                  </select>
                  <ChevronDown className="absolute right-2 top-1/2 -translate-y-1/2 h-4 w-4 text-gray-400 pointer-events-none" />
                </div>
              </div>
            </div>

            <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
              {cars === null
                ? Array.from({ length: 6 }).map((_, index) => (
                    <LoaderCard key={index} />
                  ))
                : cars.map((car) => (
                    <Link
                      key={car.id}
                      href={`/buy-car/${car.id}`}
                      className="bg-white rounded-lg shadow-md overflow-hidden hover:shadow-lg transition-shadow"
                    >
                      <div className="relative h-48">
                        <img
                          src={car.image || CAR_IMAGE_PLACEHOLDER}
                          alt={car.title}
                          className="w-full h-full object-cover"
                        />
                        <button className="absolute top-2 right-2 p-1.5 bg-white/80 rounded-full hover:bg-white">
                          <Heart className="h-4 w-4 text-gray-500 hover:text-red-500" />
                        </button>
                      </div>
                      <div className="p-4">
                        <h3 className="font-semibold text-lg mb-2">
                          {car.title}
                        </h3>
                        <div className="flex items-center justify-between mb-2">
                          <div className="text-sm text-gray-600">
                            {car.km} km
                          </div>
                          <div className="text-sm text-gray-600">
                            {car.transmission}
                          </div>
                          <div className="text-sm text-gray-600">
                            {car.fuel}
                          </div>
                          <div className="text-sm text-gray-600">
                            {car.owner}
                          </div>
                        </div>
                        <div className="flex items-center justify-between">
                          <div>
                            <div className="text-sm text-gray-600">
                              EMI from
                            </div>
                            <div className="font-semibold">{car.emi}</div>
                          </div>
                          <div className="text-right">
                            <div className="text-sm text-gray-600">Price</div>
                            <div className="font-semibold">{car.price}</div>
                            {car.recommendedPrice != null && (
                              <div className="mt-1">
                                <div className="text-xs text-gray-500">
                                  Recommended Price
                                </div>
                                <div className="font-semibold text-green-600">
                                  ₹{car.recommendedPrice.toLocaleString("en-IN")}
                                </div>
                              </div>
                            )}
                          </div>
                        </div>
                        <div className="mt-2 text-xs text-gray-500">
                          {car.location}
                        </div>
                        {car.maintenanceStatus && (
                          <div className="mt-2 text-xs">
                            <span
                              className={`inline-block px-2 py-0.5 rounded font-medium ${
                                car.maintenanceStatus === "High Maintenance Expected"
                                  ? "bg-amber-50 text-amber-700"
                                  : car.maintenanceStatus === "Moderate Maintenance"
                                  ? "bg-blue-50 text-blue-700"
                                  : "bg-green-50 text-green-700"
                              }`}
                            >
                              {car.maintenanceStatus}
                            </span>
                            {car.estimatedMonthlyMaintenance != null && (
                              <span className="ml-2 text-gray-500">
                                Est. ₹{car.estimatedMonthlyMaintenance.toLocaleString(
                                  "en-IN"
                                )}
                                /mo
                              </span>
                            )}
                          </div>
                        )}
                      </div>
                    </Link>
                  ))}
            </div>
            {cars !== null && cars.length === 0 && (
              <p className="text-center text-gray-500 mt-6">
                No cars found. Try adjusting your filters.
              </p>
            )}
          </div>
        </div>
      </div>
    </div>
  );
};

export default index;

// Small client-side geo helpers used by the "Use my location" flow.
// Coordinates come from the backend supported-city catalogue; no external
// geocoding service is called.

export interface GeoPoint {
  latitude: number;
  longitude: number;
}

// Great-circle distance in kilometers between two coordinates.
export function haversineDistanceKm(a: GeoPoint, b: GeoPoint): number {
  const toRad = (deg: number) => (deg * Math.PI) / 180;
  const earthRadiusKm = 6371;
  const dLat = toRad(b.latitude - a.latitude);
  const dLng = toRad(b.longitude - a.longitude);
  const h =
    Math.sin(dLat / 2) * Math.sin(dLat / 2) +
    Math.cos(toRad(a.latitude)) *
      Math.cos(toRad(b.latitude)) *
      Math.sin(dLng / 2) *
      Math.sin(dLng / 2);
  return 2 * earthRadiusKm * Math.asin(Math.sqrt(h));
}

// Returns the nearest supported city to the given coordinates.
export function findNearestCity<T extends GeoPoint>(
  position: GeoPoint,
  cities: T[]
): T | null {
  if (cities.length === 0) return null;
  let nearest = cities[0];
  let nearestDistance = haversineDistanceKm(position, cities[0]);
  for (const city of cities.slice(1)) {
    const distance = haversineDistanceKm(position, city);
    if (distance < nearestDistance) {
      nearest = city;
      nearestDistance = distance;
    }
  }
  return nearest;
}

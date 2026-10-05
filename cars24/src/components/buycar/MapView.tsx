import React, { useEffect } from "react";
import L from "leaflet";
import { MapContainer, Marker, Popup, TileLayer, useMap } from "react-leaflet";
import type { ServiceHub, SupportedCity } from "@/lib/Carapi";

// Restore the default Leaflet marker icons (their asset URLs are not
// resolved automatically when bundled).
const defaultIcon = new L.Icon.Default({
  iconUrl: "https://unpkg.com/leaflet@1.9.4/dist/images/marker-icon.png",
  iconRetinaUrl:
    "https://unpkg.com/leaflet@1.9.4/dist/images/marker-icon-2x.png",
  shadowUrl: "https://unpkg.com/leaflet@1.9.4/dist/images/marker-shadow.png",
});

const HUB_TYPE_LABELS: Record<string, string> = {
  ServiceCenter: "Service Center",
  PickupPoint: "Pickup Point",
  Hub: "CARS24 Hub",
};

function formatHubType(type: string): string {
  return HUB_TYPE_LABELS[type] ?? type;
}

// Recenters the map whenever the selected city changes.
function RecenterMap({ center }: { center: [number, number] }) {
  const map = useMap();
  useEffect(() => {
    map.setView(center, map.getZoom());
  }, [center, map]);
  return null;
}

interface MapViewProps {
  city: SupportedCity;
  hubs: ServiceHub[];
}

// Interactive OpenStreetMap view showing the service centers, pickup
// points and hubs of the selected city. Rendered client-side only.
const MapView: React.FC<MapViewProps> = ({ city, hubs }) => {
  const center: [number, number] = [city.latitude, city.longitude];
  return (
    <MapContainer
      center={center}
      zoom={11}
      scrollWheelZoom
      style={{ height: "100%", width: "100%" }}
    >
      <RecenterMap center={center} />
      <TileLayer
        attribution='&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors'
        url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png"
      />
      {hubs.map((hub) => (
        <Marker
          key={`${hub.name}-${hub.latitude}-${hub.longitude}`}
          position={[hub.latitude, hub.longitude]}
          icon={defaultIcon}
        >
          <Popup>
            <div className="text-sm">
              <p className="font-semibold">{hub.name}</p>
              <p>{formatHubType(hub.type)}</p>
              <p>{hub.address}</p>
            </div>
          </Popup>
        </Marker>
      ))}
    </MapContainer>
  );
};

export default MapView;

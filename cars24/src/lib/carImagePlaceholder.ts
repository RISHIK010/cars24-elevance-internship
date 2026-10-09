// Honest, self-contained placeholder used whenever a car has no photo.
// An inline SVG data URI keeps it dependency-free and renderable in a plain
// <img src>, so the listing and detail views never show a broken image or a
// random catalogue photo.
export const CAR_IMAGE_PLACEHOLDER = `data:image/svg+xml,${encodeURIComponent(
  `<svg xmlns="http://www.w3.org/2000/svg" width="800" height="450" viewBox="0 0 800 450">
    <rect width="800" height="450" fill="#e5e7eb"/>
    <g fill="#d1d5db">
      <path d="M180 200h440v130c0 22-18 40-40 40H220c-22 0-40-18-40-40V200z"/>
      <path d="M260 150h280c22 0 40 18 40 40v50H220v-50c0-22 18-40 40-40z"/>
      <circle cx="280" cy="340" r="38"/>
      <circle cx="520" cy="340" r="38"/>
    </g>
    <text x="400" y="412" font-family="Arial, Helvetica, sans-serif" font-size="28" fill="#6b7280" text-anchor="middle">No photo available</text>
  </svg>`
)}`;

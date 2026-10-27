// Shared concept-art sprite. Injected so each artboard stays a single readable HTML file.
document.body.insertAdjacentHTML(
  'afterbegin',
  `<svg xmlns="http://www.w3.org/2000/svg" aria-hidden="true" style="position:absolute;width:0;height:0;overflow:hidden">
  <symbol id="i-counter" viewBox="0 0 24 24">
    <path d="M5 8.5h11v5.2a4.8 4.8 0 0 1-4.8 4.8H9.8A4.8 4.8 0 0 1 5 13.7V8.5Z"/>
    <path d="M16 10h1.6a2.9 2.9 0 0 1 0 5.8H16"/>
    <path d="M7.5 4.5c.4 1-.4 1.8 0 2.6M11 4.5c.4 1-.4 1.8 0 2.6"/>
  </symbol>
  <symbol id="i-board" viewBox="0 0 24 24">
    <rect x="3.5" y="4" width="17" height="16" rx="2.2"/>
    <path d="M9.2 4v16M14.8 4v16M5.6 8h1.6M11.2 8h1.6M16.8 8h1.6"/>
  </symbol>
  <symbol id="i-menu" viewBox="0 0 24 24">
    <path d="M4 5.5c2.7-.8 5.3-.8 8 0v13c-2.7-.8-5.3-.8-8 0v-13ZM12 5.5c2.7-.8 5.3-.8 8 0v13c-2.7-.8-5.3-.8-8 0v-13Z"/>
    <path d="M6.5 9h3M14.5 9h3M6.5 12h2M14.5 12h2"/>
  </symbol>
  <symbol id="i-stock" viewBox="0 0 24 24">
    <path d="M4 8.5 12 4l8 4.5v7L12 20l-8-4.5v-7Z"/>
    <path d="M4 8.5 12 13l8-4.5M12 13v7"/>
  </symbol>
  <symbol id="i-customers" viewBox="0 0 24 24">
    <circle cx="9" cy="8" r="3"/>
    <path d="M3.5 19c.4-3.2 2.4-5 5.5-5s5.1 1.8 5.5 5"/>
    <circle cx="17" cy="9" r="2.2"/>
    <path d="M15.5 14.5c2.8-.6 4.7 1 5 3.8"/>
  </symbol>
  <symbol id="i-reports" viewBox="0 0 24 24">
    <path d="M4 19V5M4 19h16"/>
    <path d="M7.5 15v-4M11.5 15V8M15.5 15v-2.5M19 15V10"/>
  </symbol>
  <symbol id="i-search" viewBox="0 0 24 24">
    <circle cx="10.5" cy="10.5" r="5.5"/>
    <path d="m15 15 4 4"/>
  </symbol>
  <symbol id="i-bell" viewBox="0 0 24 24">
    <path d="M6.5 17h11c-1-1.3-1.4-2.5-1.4-4.4v-1.3a4.1 4.1 0 0 0-8.2 0v1.3c0 1.9-.4 3.1-1.4 4.4Z"/>
    <path d="M10.3 19.5a2 2 0 0 0 3.4 0M12 4v1.2"/>
  </symbol>
  <symbol id="i-plus" viewBox="0 0 24 24">
    <path d="M12 5v14M5 12h14"/>
  </symbol>
  <symbol id="i-arrow" viewBox="0 0 24 24">
    <path d="M5 12h13M14 7l5 5-5 5"/>
  </symbol>
  <symbol id="i-refresh" viewBox="0 0 24 24">
    <path d="M19 9.5A7.5 7.5 0 1 0 19.5 15"/>
    <path d="M19 4.5v5h-5"/>
  </symbol>
  <symbol id="i-clock" viewBox="0 0 24 24">
    <circle cx="12" cy="12" r="8"/>
    <path d="M12 7.5V12l3 2"/>
  </symbol>
  <symbol id="i-table" viewBox="0 0 24 24">
    <path d="M12 5.5a6 6 0 1 0 0 12a6 6 0 0 0 0-12Z"/>
    <path d="M12 17.5V21M8.5 21h7M8 9.5c2.5 1.2 5.5 1.2 8 0"/>
  </symbol>
  <symbol id="i-bag" viewBox="0 0 24 24">
    <path d="M6 8h12l-1 12H7L6 8Z"/>
    <path d="M9 8V6a3 3 0 0 1 6 0v2"/>
  </symbol>
  <symbol id="i-bike" viewBox="0 0 24 24">
    <circle cx="6.5" cy="16.5" r="3"/>
    <circle cx="17.5" cy="16.5" r="3"/>
    <path d="M6.5 16.5h4l3-7h-3M10.5 16.5l3-7 4 7M13 6h3M15 13h3"/>
  </symbol>
  <symbol id="i-cash" viewBox="0 0 24 24">
    <rect x="3" y="6.5" width="18" height="11" rx="1.6"/>
    <circle cx="12" cy="12" r="2.4"/>
    <path d="M6 9.5v5M18 9.5v5"/>
  </symbol>
  <symbol id="i-card" viewBox="0 0 24 24">
    <rect x="3" y="5.5" width="18" height="13" rx="2"/>
    <path d="M3 9.5h18M6.5 14.5h4"/>
  </symbol>
  <symbol id="i-wallet" viewBox="0 0 24 24">
    <path d="M4 7.5A2.5 2.5 0 0 1 6.5 5H18v3"/>
    <rect x="4" y="7.5" width="16" height="11" rx="2"/>
    <path d="M16 12h2.5M16 12.2v2.6"/>
  </symbol>
  <symbol id="i-check" viewBox="0 0 24 24">
    <path d="m5 12.5 4.2 4L19 7"/>
  </symbol>
  <symbol id="i-x" viewBox="0 0 24 24">
    <path d="m6 6 12 12M18 6 6 18"/>
  </symbol>
  <symbol id="i-edit" viewBox="0 0 24 24">
    <path d="m4 20 1-4 10.5-10.5a2.1 2.1 0 0 1 3 3L8 19l-4 1Z"/>
    <path d="m14 7 3 3"/>
  </symbol>
  <symbol id="i-dots" viewBox="0 0 24 24">
    <circle cx="6" cy="12" r="1"/><circle cx="12" cy="12" r="1"/><circle cx="18" cy="12" r="1"/>
  </symbol>
  <symbol id="i-tag" viewBox="0 0 24 24">
    <path d="M4 4h7l9 9-7 7-9-9V4Z"/>
    <circle cx="8.5" cy="8.5" r="1.2"/>
  </symbol>
  <symbol id="i-leaf" viewBox="0 0 24 24">
    <path d="M19 5c-7.5-.5-12 3-12 8 0 3 2 5 5 5 5 0 7.5-5.5 7-13Z"/>
    <path d="M4 20c2-5 5-8 9-10"/>
  </symbol>
  <symbol id="i-home" viewBox="0 0 24 24">
    <path d="M4 11 12 4l8 7v8h-6v-5h-4v5H4v-8Z"/>
  </symbol>
  <symbol id="i-tea" viewBox="0 0 24 24">
    <path d="M5 9h11v4.5a4.5 4.5 0 0 1-4.5 4.5H9.5A4.5 4.5 0 0 1 5 13.5V9Z"/>
    <path d="M16 10.5h1.4a2.4 2.4 0 0 1 0 4.8H16M8 21h10M14 3l3-2 1.5 2.5"/>
  </symbol>
  <symbol id="i-croissant" viewBox="0 0 24 24">
    <path d="M4 15c0-5 3.5-8 8-8s8 3 8 8c-2.5-1-4-1-5.5 0-1-1.5-3.5-1.5-5 0C8 14 6 14 4 15Z"/>
    <path d="M8.5 8.5 10 12M15.5 8.5 14 12"/>
  </symbol>
  <symbol id="i-filter" viewBox="0 0 24 24">
    <path d="M4 5h16l-6 7v6l-4 2v-8L4 5Z"/>
  </symbol>
  <symbol id="i-calendar" viewBox="0 0 24 24">
    <rect x="4" y="5.5" width="16" height="14" rx="1.6"/>
    <path d="M4 9.5h16M8 3.5v4M16 3.5v4M8 13h2M13 13h2M8 16.5h2"/>
  </symbol>
  <symbol id="i-download" viewBox="0 0 24 24">
    <path d="M12 4v11M7.5 11 12 15.5 16.5 11M5 19h14"/>
  </symbol>
  <symbol id="i-chevron" viewBox="0 0 24 24">
    <path d="m7 9.5 5 5 5-5"/>
  </symbol>
  <symbol id="i-warning" viewBox="0 0 24 24">
    <path d="M12 4 3.8 19h16.4L12 4Z"/>
    <path d="M12 9v4.5M12 16.5v.5"/>
  </symbol>
  <symbol id="i-note" viewBox="0 0 24 24">
    <path d="M5 4.5h14v11l-4 4H5v-15Z"/>
    <path d="M15 19.5v-4h4M8.5 8.5h7M8.5 11.5h5"/>
  </symbol>
  <symbol id="i-settings" viewBox="0 0 24 24">
    <path d="M4 7h16M4 12h16M4 17h16"/>
    <circle cx="9" cy="7" r="2"/><circle cx="15" cy="12" r="2"/><circle cx="10" cy="17" r="2"/>
  </symbol>
  <symbol id="i-power" viewBox="0 0 24 24">
    <path d="M12 4v7"/>
    <path d="M7.5 6.5a7 7 0 1 0 9 0"/>
  </symbol>
  <symbol id="i-receipt" viewBox="0 0 24 24">
    <path d="M6 3.5h12v17l-3-2-3 2-3-2-3 2v-17Z"/>
    <path d="M9 8h6M9 11.5h6M9 15h3"/>
  </symbol>
  <symbol id="d-espresso" viewBox="0 0 64 64">
    <path d="M13 27h31v8a14 14 0 0 1-14 14h-3a14 14 0 0 1-14-14v-8Z" fill="none"/>
    <path d="M44 30h4.5a7 7 0 0 1 0 14H44" fill="none"/>
    <path d="M17 55h30" stroke-width="2.4"/>
    <path d="M24 20c1.5-3-1.5-4.5 0-8M32 20c1.5-3-1.5-4.5 0-8M40 20c1.5-3-1.5-4.5 0-8" opacity=".6"/>
    <path d="M18 34c7 4 14 4 21 0" opacity=".4"/>
  </symbol>
  <symbol id="d-iced" viewBox="0 0 64 64">
    <path d="M19 16h26l-3 36H22l-3-36Z" fill="none"/>
    <path d="M17 24h30" opacity=".5"/>
    <path d="M39 8 29 25" stroke-width="2.6"/>
    <path d="M25 31h7v7h-7zM34 36h6v6h-6zM25 43h6v5h-6z" opacity=".55"/>
    <path d="M22 58h24" stroke-width="2.4"/>
    <path d="M21 34c5 3 13 3 19 0" opacity=".35"/>
  </symbol>
  <symbol id="d-tea" viewBox="0 0 64 64">
    <path d="M14 24h30v11a13 13 0 0 1-13 13h-4a13 13 0 0 1-13-13V24Z" fill="none"/>
    <path d="M44 28h4a6 6 0 0 1 0 12h-4" fill="none"/>
    <path d="M19 54h26M29 15l8-8 4 1" opacity=".6"/>
    <path d="M45 6l4 3-3 5" opacity=".6"/>
    <path d="M20 32c5 3 12 3 17 0" opacity=".35"/>
  </symbol>
  <symbol id="d-pastry" viewBox="0 0 64 64">
    <path d="M11 39c0-14 9-22 21-22s21 8 21 22c-6-3-10-3-14 0-2-4-9-4-12 0-4-3-9-3-16 0Z" fill="none"/>
    <path d="M22 21l4 12M33 18v14M43 21l-4 12" opacity=".6"/>
    <path d="M8 46c8 5 15 5 22 1 8 4 16 4 26-1" stroke-width="2.2" opacity=".55"/>
  </symbol>
  <symbol id="d-matcha" viewBox="0 0 64 64">
    <path d="M13 28h32v8a16 16 0 0 1-16 16 16 16 0 0 1-16-16v-8Z" fill="none"/>
    <path d="M17 55h26M29 12v16M35 12v16M29 12l6 16M22 20c4 2 8 2 12 0" opacity=".6"/>
    <path d="M18 35c6 4 15 4 21 0" opacity=".35"/>
  </symbol>
  <symbol id="d-bag" viewBox="0 0 64 64">
    <path d="M18 20h28l-2 32H20l-2-32Z" fill="none"/>
    <path d="M26 20v-4a6 6 0 0 1 12 0v4" fill="none"/>
    <path d="M25 32h14M25 38h10" opacity=".55"/>
  </symbol>
</svg>`
);

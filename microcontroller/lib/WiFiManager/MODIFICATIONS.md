# Local modifications to WiFiManager

This is a **vendored, locally-modified copy** of tzapu's WiFiManager, kept in `lib/` (not
pulled via `lib_deps`) precisely because it is patched. Do not replace it with the upstream
registry version — that would drop the changes below.

- **Forked from:** tzapu/WiFiManager **v2.0.17** (https://github.com/tzapu/WiFiManager, tag `v2.0.17`)
- **`library.json` / `library.properties` version is left at `2.0.17`** so the base version is
  traceable; the patch lives only in the file noted below.

## What was changed

Exactly **one** file differs from upstream v2.0.17. The other sources
(`WiFiManager.cpp`, `WiFiManager.h`, `strings_en.h`, `wm_consts_en.h`, `wm_strings_es.h`)
are byte-for-byte identical to upstream — verify any time with:

```sh
git clone --depth 1 --branch v2.0.17 https://github.com/tzapu/WiFiManager.git /tmp/wm-upstream
diff -r --strip-trailing-cr /tmp/wm-upstream lib/WiFiManager   # only wm_strings_en.h should differ
```

### `wm_strings_en.h` — custom config-portal UI

The portal HTML/CSS/JS strings were rewritten into a stripped-down, phone-friendly dark
theme. No C/C++ logic was touched, only the `PROGMEM` markup/style constants. Specifically:

- **Dark theme** (`HTTP_STYLE`): full replacement of the stock light stylesheet with a
  compact dark one (`#111` background, rounded card-style network list, large touch targets,
  system font). Removed the RSSI signal-strength icons and their base64 sprite/`@2x` media
  query, the `.msg` callout styling, list styling, and the `body.invert` rules.
- **Streamlined flow:**
  - `HTTP_ROOT_MAIN` now just redirects to `/wifi` (`location.replace('/wifi')`) so the menu
    page is skipped — the user lands straight on the network picker.
  - `HTTP_SCRIPT` (`c()` onclick): instead of toggling a password field, selecting a network
    now hides the network list (`.wl`) and the "Choose WiFi" title, fills in the SSID name,
    and reveals just the password input + a single **Connect** button.
  - `HTTP_HEAD_END` adds an `<h2 id='hc-title'>Choose WiFi</h2>` heading.
  - `HTTP_FORM_WIFI` / `HTTP_FORM_END`: SSID becomes a hidden field, password input starts
    hidden, submit button relabeled **Connect** and hidden until a network is chosen.
- **Removed UI elements** (set to empty strings): RSSI quality items (`HTTP_ITEM_QI`,
  `HTTP_ITEM_QP`), the Refresh/scan link (`HTTP_SCAN_LINK`), the Back button (`HTTP_BACKBTN`),
  and the "No AP set" status (`HTTP_STATUS_NONE`). `h1/h3/hr/br` are hidden via CSS.

Net effect: scanning for a network, tapping it, typing the password and pressing Connect —
a minimal captive-portal experience for the `SpeedSeat-Setup` access point.

## How it's wired in

`microcontroller/src/transport.cpp` (`UdpTransport::begin`) calls `wm.autoConnect("SpeedSeat-Setup")`,
which opens this portal whenever no saved WiFi network is reachable.

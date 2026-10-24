# WindowsMender v1.0.0

![WindowsMender UI Preview](assets/preview.png)

A modern, lightweight WPF desktop application designed for Windows OS diagnostics, WinSxS component store repair, and update troubleshooting.

## Features
- **Admin Elevation Check:** Automatic UI prompt to restart with administrator privileges when needed.
- **Service Diagnostics:** Inspects and controls core update services (`wuauserv`, `bits`, `cryptsvc`, `msiserver`).
- **Event Log Reader:** Scans for recent Windows Update failure entries.
- **Cache Cleaner:** Safely stops services and clears corrupted `SoftwareDistribution` and `Catroot2` caches.
- **Component Store Repair:** Executes DISM `RestoreHealth` with clear progress feedback and speed/corruption guidance.
- **Dark Mode UI:** Clean, responsive WPF user interface with text-wrapped logging and interactive tooltips.

## License
Distributed under the MIT License. See `LICENSE` for details.
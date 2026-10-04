# ArborGraph Hardware, Platform & Display Compatibility Matrix

**Document Identifier:** AG-CP-001  
**Target Release:** v1.0.0  
**Target Architectures:** Windows x64 (`net8.0-windows`)  
**Status Legend:**  
- `TESTED`: Formally verified on hardware  
- `NOT TESTED`: Planned test environment awaiting execution  
- `NOT APPLICABLE`: Configuration intentionally unsupported or out of scope  

---

## 1. Operating System & Hardware Environments

| Environment ID | Specification Description | Primary Focus Areas | Status | Notes |
| :--- | :--- | :--- | :--- | :--- |
| **ENV-A** | **Developer / Reference Machine**<br>Windows 11 (24H2 / Build 26100)<br>AMD Ryzen 7 / Intel Core i7, 32 GB RAM, NVMe PCIe 4.0 SSD | Reference baseline, max throughput, developer tooling | `NOT TESTED` | Baseline execution environment for integration tests |
| **ENV-B** | **Mid-Range Commercial PC**<br>Windows 10 (22H2 / Build 19045)<br>Intel Core i5 (8th–11th Gen), 16 GB RAM, SATA SSD | Standard enterprise & consumer deployment profile | `NOT TESTED` | Verifies Windows 10 WPF theme rendering & USN Journal compatibility |
| **ENV-C** | **Resource-Constrained Laptop**<br>Windows 10 / 11 (64-bit)<br>Intel Core i3 / Celeron, 8 GB RAM, Mechanical 5400 RPM HDD | Low memory scaling, LOH rollup GC behavior, high disk latency | `NOT TESTED` | Crucial for validating `BuildDirectoryRollup` under tight RAM |
| **ENV-D** | **Virtual Machine (Clean Hyper-V / VirtualBox)**<br>Windows 11 Home (64-bit)<br>Fresh OS installation, zero development SDKs | Missing dependency validation, Inno Setup lowest privilege gate | `NOT TESTED` | Verifies application startup without pre-installed developer tools |
| **ENV-E** | **macOS / Linux (Wine / Proton)**<br>Non-Windows platforms | Out of scope | `NOT APPLICABLE` | ArborGraph is strictly native Windows WPF (`net8.0-windows`) |

---

## 2. High-DPI Display Scaling Matrix

WPF applications rendering custom framework elements (`RealtimeMetricGraph`) and vector canvases (`TreemapView`) must scale cleanly across Windows display scaling settings without text clipping, blurry rasterization, or coordinate misalignments:

| Scale Factor | Resolution | Target Form Factor | Expected Behavior | Status |
| :--- | :--- | :--- | :--- | :--- |
| **100% (96 DPI)** | 1920 x 1080 | Standard 24" Desktop Monitor | 1:1 pixel rendering, sharp borders, correct sparkline line width | `NOT TESTED` |
| **125% (120 DPI)** | 1920 x 1080 / 2560 x 1440 | 14"–15" Laptops | Scaled typography, no button text clipping, navigation tabs visible | `NOT TESTED` |
| **150% (144 DPI)** | 2560 x 1440 / 3840 x 2160 | 13" Laptops / 27" 4K Displays | Responsive layout adapts; treemap canvas computes proportional bounding rects | `NOT TESTED` |
| **175% (168 DPI)** | 3840 x 2160 | High-density 15" Laptops | Overview storage cards wrap gracefully; dialog boxes fit screen | `NOT TESTED` |
| **200% (192 DPI)** | 3840 x 2160 | 4K Displays / Microsoft Surface | Vector icons render crisp; status bar text remains vertically aligned | `NOT TESTED` |

---

## 3. Storage Hardware & Filesystem Matrix

| Storage Type | Filesystem Format | Special Characteristics to Test | Expected Behavior | Status |
| :--- | :--- | :--- | :--- | :--- |
| **NVMe PCIe SSD** | NTFS | High IOPS, active NTFS USN Change Journal | Max throughput (>= 25,000 files/sec); incremental scan in milliseconds | `NOT TESTED` |
| **SATA SSD** | NTFS | Typical consumer SSD | Stable traversal without channel buffer starvation | `NOT TESTED` |
| **Mechanical HDD (Spinning)** | NTFS | High seek latency, slow directory enumeration | Scanner throughput gracefully degrades; UI remains fluid | `NOT TESTED` |
| **USB Flash Drive** | exFAT | Non-NTFS volume, removable media | USN journal query detects non-NTFS; automatically falls back to full scan | `NOT TESTED` |
| **Legacy Media** | FAT32 | 4GB file size limit, non-NTFS | Accurate file parsing, proper detection of volume format | `NOT TESTED` |
| **BitLocker Encrypted Volume** | NTFS | Transparent on-the-fly encryption | Traversal operates normally on unlocked volume | `NOT TESTED` |
| **Mapped Network Drive (SMB/UNC)** | SMB / Remote NTFS | Network latency, partial disconnection | Gracefully handles network timeouts; skips inaccessible remote folders | `NOT TESTED` |
| **Multi-Partition Drive Setup** | Multiple NTFS partitions (C: and D:) | Distinct volume handles and roots | `ClearIndex` preserves each volume's index without cross-volume wiping | `NOT TESTED` |
| **Cloud Files-on-Demand (OneDrive)** | NTFS with Reparse Points | Cloud placeholder attributes (`RecallOnDataAccess`) | Scanner skips placeholder bodies; **zero** network hydration triggered | `NOT TESTED` |

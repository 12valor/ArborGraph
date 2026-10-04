// =============================================================================
// ArborGraph: Official Web Application Portal & Diagnostic Simulation
// Source of Truth: Desktop Application UI (C# WPF / .NET 8)
// RealtimeMetricGraph sparklines, Scanner traversal, Treemap drilldown, Inno Setup
// =============================================================================

document.addEventListener('DOMContentLoaded', () => {
    'use strict';

    const OFFICIAL_HASH = '31AC3B1DB800A368B92B243660DA12A7AF7B6DCA5327C8C2C99AA431540B0D85';

    // =========================================================================
    // 1. TOAST NOTIFICATION HELPER
    // =========================================================================
    const toast = document.getElementById('toastMsg');
    let toastTimeout = null;

    function showToast(message) {
        if (!toast) return;
        toast.textContent = message;
        toast.classList.add('show');
        clearTimeout(toastTimeout);
        toastTimeout = setTimeout(() => {
            toast.classList.remove('show');
        }, 3000);
    }

    // =========================================================================
    // 2. HEADER SCROLL & MOBILE DRAWER NAVIGATION
    // =========================================================================
    const siteHeader = document.getElementById('siteHeader');
    const mobileNavToggle = document.getElementById('mobileNavToggle');
    const mobileNavDrawer = document.getElementById('mobileNavDrawer');
    const mobileNavLinks = document.querySelectorAll('.mobile-nav-link');
    const navLinks = document.querySelectorAll('.site-nav .nav-link');
    const sections = document.querySelectorAll('section.site-section');

    window.addEventListener('scroll', () => {
        if (siteHeader) {
            siteHeader.classList.toggle('scrolled', window.scrollY > 15);
        }
    }, { passive: true });

    if (mobileNavToggle && mobileNavDrawer) {
        mobileNavToggle.addEventListener('click', () => {
            mobileNavDrawer.classList.toggle('open');
        });

        mobileNavLinks.forEach(link => {
            link.addEventListener('click', () => {
                mobileNavDrawer.classList.remove('open');
            });
        });
    }

    // Scrollspy navigation link tracking
    if ('IntersectionObserver' in window && sections.length > 0) {
        const observer = new IntersectionObserver((entries) => {
            entries.forEach(entry => {
                if (entry.isIntersecting) {
                    const id = entry.target.getAttribute('id');
                    navLinks.forEach(link => {
                        const href = link.getAttribute('href');
                        link.classList.toggle('active', href === `#${id}`);
                    });
                }
            });
        }, { root: null, rootMargin: '-20% 0px -60% 0px', threshold: 0 });

        sections.forEach(sec => observer.observe(sec));
    }

    // =========================================================================
    // 3. SYSTEM OS & ARCHITECTURE DIAGNOSTICS
    // =========================================================================
    const systemDiagStatus = document.getElementById('systemDiagStatus');
    if (systemDiagStatus) {
        const ua = window.navigator.userAgent;
        if (/Win64|x64|WOW64/i.test(ua)) {
            systemDiagStatus.textContent = 'SYSTEM READY: Windows 64-bit Architecture Verified';
        } else if (/Windows/i.test(ua)) {
            systemDiagStatus.textContent = 'SYSTEM READY: Windows OS Architecture Detected';
        } else if (/Mac/i.test(ua)) {
            systemDiagStatus.textContent = 'NOTICE: macOS Detected. ArborGraph is a native Windows x64 utility.';
        } else if (/Linux/i.test(ua)) {
            systemDiagStatus.textContent = 'NOTICE: Linux Detected. ArborGraph is a native Windows x64 utility.';
        } else {
            systemDiagStatus.textContent = 'SYSTEM READY: Windows 10 / 11 (64-bit) Compatible';
        }
    }

    // =========================================================================
    // 4. REAL-TIME CPU & RAM METRIC SPARKLINES
    // Replicating Controls/RealtimeMetricGraph.cs (60-point buffer, 42px height,
    // subtle dashed horizontal guides at 25%, 50%, 75%, accent & slate strokes)
    // =========================================================================
    const cpuCanvas = document.getElementById('cpuCanvas');
    const ramCanvas = document.getElementById('ramCanvas');
    const cpuValText = document.getElementById('cpuValText');
    const cpuPeakText = document.getElementById('cpuPeakText');
    const ramValText = document.getElementById('ramValText');
    const ramCeilingText = document.getElementById('ramCeilingText');

    const BUFFER_CAPACITY = 60;
    const cpuBuffer = new Float32Array(BUFFER_CAPACITY);
    const ramBuffer = new Float32Array(BUFFER_CAPACITY);

    // Initial baseline data points (idle state)
    for (let i = 0; i < BUFFER_CAPACITY; i++) {
        cpuBuffer[i] = 7 + Math.sin(i * 0.4) * 3 + Math.random() * 2;
        ramBuffer[i] = 180 + Math.sin(i * 0.2) * 4 + Math.random() * 2;
    }

    let cpuPeak = 14.8;
    const RAM_CEILING = 1024; // MB
    let isCurrentlyScanning = false;

    function renderSparkline(canvas, buffer, color, maxValue, isPercent) {
        if (!canvas) return;
        const ctx = canvas.getContext('2d');
        if (!ctx) return;

        const w = canvas.width;
        const h = canvas.height;

        ctx.clearRect(0, 0, w, h);

        // 1. Subtle horizontal guidelines (25%, 50%, 75%) matching WPF RealtimeMetricGraph
        ctx.strokeStyle = '#E5E7EB';
        ctx.lineWidth = 1;
        ctx.setLineDash([2, 4]);

        const guides = [0.25, 0.50, 0.75];
        guides.forEach(ratio => {
            const y = Math.round(h * (1 - ratio));
            ctx.beginPath();
            ctx.moveTo(0, y);
            ctx.lineTo(w, y);
            ctx.stroke();
        });

        ctx.setLineDash([]); // Reset dash

        // 2. Draw graph polyline
        ctx.strokeStyle = color;
        ctx.lineWidth = 1.5;
        ctx.lineJoin = 'round';
        ctx.lineCap = 'round';
        ctx.beginPath();

        const step = w / (BUFFER_CAPACITY - 1);
        for (let i = 0; i < BUFFER_CAPACITY; i++) {
            const val = buffer[i];
            const ratio = Math.min(1, Math.max(0, val / maxValue));
            const x = i * step;
            const y = h - (ratio * (h - 4)) - 2; // leave 2px padding

            if (i === 0) {
                ctx.moveTo(x, y);
            } else {
                ctx.lineTo(x, y);
            }
        }
        ctx.stroke();

        // 3. Subtle gradient fill under the line
        ctx.lineTo(w, h);
        ctx.lineTo(0, h);
        ctx.closePath();
        const grad = ctx.createLinearGradient(0, 0, 0, h);
        grad.addColorStop(0, color === '#005FB8' ? 'rgba(0, 95, 184, 0.12)' : 'rgba(75, 85, 99, 0.10)');
        grad.addColorStop(1, 'rgba(255, 255, 255, 0)');
        ctx.fillStyle = grad;
        ctx.fill();
    }

    // Sparkline continuous telemetry update loop
    setInterval(() => {
        // Shift left
        for (let i = 0; i < BUFFER_CAPACITY - 1; i++) {
            cpuBuffer[i] = cpuBuffer[i + 1];
            ramBuffer[i] = ramBuffer[i + 1];
        }

        // Generate next sample based on scan load
        let nextCpu, nextRam;
        if (isCurrentlyScanning) {
            nextCpu = 32 + Math.random() * 22; // 32% - 54% during disk traversal
            nextRam = 310 + Math.random() * 45; // 310 MB - 355 MB during scan
        } else {
            nextCpu = 8 + Math.random() * 6;   // 8% - 14% idle
            nextRam = 182 + Math.random() * 5; // 182 MB - 187 MB idle
        }

        cpuBuffer[BUFFER_CAPACITY - 1] = nextCpu;
        ramBuffer[BUFFER_CAPACITY - 1] = nextRam;

        if (nextCpu > cpuPeak) cpuPeak = nextCpu;

        // Render sparkline canvases
        renderSparkline(cpuCanvas, cpuBuffer, '#005FB8', 100, true);
        renderSparkline(ramCanvas, ramBuffer, '#4B5563', RAM_CEILING, false);

        // Update textual readouts
        if (cpuValText) cpuValText.textContent = `${nextCpu.toFixed(1)}%`;
        if (cpuPeakText) cpuPeakText.textContent = `Peak: ${cpuPeak.toFixed(1)}%`;
        if (ramValText) ramValText.textContent = `${Math.round(nextRam)} MB`;
        if (ramCeilingText) ramCeilingText.textContent = `Ceiling: ${RAM_CEILING} MB`;
    }, 1000);

    // Initial render
    renderSparkline(cpuCanvas, cpuBuffer, '#005FB8', 100, true);
    renderSparkline(ramCanvas, ramBuffer, '#4B5563', RAM_CEILING, false);

    // =========================================================================
    // 5. DEMO UI 02: WINDOWS INNO SETUP WIZARD WITH STRICT EULA GATE
    // Matching installer/installer.iss structure (Steps 1 to 7)
    // =========================================================================
    let wizardCurrentStep = 1;
    const totalWizardSteps = 7;

    const wizardBannerTitles = [
        "Setup — ArborGraph",
        "License Agreement",
        "Select Destination Location",
        "Select Additional Tasks",
        "Ready to Install",
        "Installing",
        "Completing the ArborGraph Setup Wizard"
    ];

    const wizardBannerSubtitles = [
        "Welcome to the ArborGraph Setup Wizard",
        "Please read the following important information before continuing.",
        "Where should ArborGraph be installed?",
        "Which additional tasks should be performed?",
        "Setup is now ready to begin installing ArborGraph on your computer.",
        "Please wait while Setup installs ArborGraph on your computer.",
        "Setup has finished installing ArborGraph on your computer."
    ];

    const wizardBannerTitle = document.getElementById('wizardBannerTitle');
    const wizardBannerSubtitle = document.getElementById('wizardBannerSubtitle');
    const wizardStepBadge = document.getElementById('wizardStepBadge');
    const wizardStepNodes = document.querySelectorAll('.wizard-step-node');
    const wizardPanes = document.querySelectorAll('.wizard-pane');
    const btnWizardBack = document.getElementById('btnWizardBack');
    const btnWizardNext = document.getElementById('btnWizardNext');
    const btnWizardCancel = document.getElementById('btnWizardCancel');
    const wizardEulaCheck = document.getElementById('wizardEulaCheck');
    const wizardGateWarning = document.getElementById('wizardGateWarning');
    const btnTogglePortablePath = document.getElementById('btnTogglePortablePath');
    const wizardInstallPath = document.getElementById('wizardInstallPath');
    const summaryLocation = document.getElementById('summaryLocation');
    const wizardProgressFill = document.getElementById('wizardProgressFill');
    const wizardExtractStatus = document.getElementById('wizardExtractStatus');
    const wizardLogText = document.getElementById('wizardLogText');
    const wizardLaunchApp = document.getElementById('wizardLaunchApp');
    const consentSummaryDetails = document.getElementById('consentSummaryDetails');

    function updateWizardUI() {
        const stepIdx = wizardCurrentStep - 1;

        // 1. Update banner text
        if (wizardBannerTitle) wizardBannerTitle.textContent = wizardBannerTitles[stepIdx];
        if (wizardBannerSubtitle) wizardBannerSubtitle.textContent = wizardBannerSubtitles[stepIdx];

        // 2. Update step badge
        if (wizardStepBadge) {
            wizardStepBadge.textContent = `Step ${wizardCurrentStep} of ${totalWizardSteps}: ${wizardBannerTitles[stepIdx]}`;
        }

        // 3. Update top stepper strip
        wizardStepNodes.forEach(node => {
            const stepNum = parseInt(node.getAttribute('data-step'), 10);
            node.classList.toggle('active', stepNum === wizardCurrentStep);
            node.classList.toggle('completed', stepNum < wizardCurrentStep);
        });

        // 4. Switch active pane
        wizardPanes.forEach(pane => {
            pane.classList.toggle('active', pane.id === `wizardStep${wizardCurrentStep}`);
        });

        // 5. Back button state
        if (btnWizardBack) {
            btnWizardBack.disabled = (wizardCurrentStep === 1 || wizardCurrentStep === 6 || wizardCurrentStep === 7);
        }

        // 6. Next / Install / Finish button state & text
        if (btnWizardNext) {
            if (wizardCurrentStep === 2) {
                btnWizardNext.textContent = "Next >";
                btnWizardNext.disabled = !wizardEulaCheck.checked;
            } else if (wizardCurrentStep === 5) {
                btnWizardNext.textContent = "Install";
                btnWizardNext.disabled = false;
            } else if (wizardCurrentStep === 6) {
                btnWizardNext.textContent = "Installing...";
                btnWizardNext.disabled = true;
            } else if (wizardCurrentStep === 7) {
                btnWizardNext.textContent = "Finish";
                btnWizardNext.disabled = false;
            } else {
                btnWizardNext.textContent = "Next >";
                btnWizardNext.disabled = false;
            }
        }

        // 7. Ready step summary
        if (wizardCurrentStep === 5 && summaryLocation && wizardInstallPath) {
            summaryLocation.textContent = wizardInstallPath.value;
        }

        // 8. Extraction animation on Step 6
        if (wizardCurrentStep === 6) {
            startExtractionSimulation();
        }

        // 9. Completion consent record
        if (wizardCurrentStep === 7) {
            recordOfflineConsent();
        }
    }

    // EULA Acceptance listener
    if (wizardEulaCheck) {
        wizardEulaCheck.addEventListener('change', () => {
            const isAccepted = wizardEulaCheck.checked;
            if (wizardCurrentStep === 2 && btnWizardNext) {
                btnWizardNext.disabled = !isAccepted;
            }
            if (wizardGateWarning) {
                wizardGateWarning.classList.toggle('accepted', isAccepted);
                wizardGateWarning.innerHTML = isAccepted
                    ? '<svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5"><polyline points="20 6 9 17 4 12"></polyline></svg><span>Terms accepted. You may now continue.</span>'
                    : '<svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="12" cy="12" r="10"></circle><line x1="12" y1="8" x2="12" y2="12"></line><line x1="12" y1="16" x2="12.01" y2="16"></line></svg><span>Acceptance required. The Next button is disabled until accepted.</span>';
            }
        });
    }

    // Next / Install / Finish button click
    if (btnWizardNext) {
        btnWizardNext.addEventListener('click', () => {
            if (wizardCurrentStep === 2 && !wizardEulaCheck.checked) {
                showToast('Please check the agreement box to accept the EULA.');
                return;
            }

            if (wizardCurrentStep === 7) {
                if (wizardLaunchApp && wizardLaunchApp.checked) {
                    showToast('Downloading ArborGraph.exe...');
                    const link = document.createElement('a');
                    link.href = 'https://github.com/12valor/ArborGraph/releases/latest/download/ArborGraph.exe';
                    link.target = '_blank';
                    link.rel = 'noopener noreferrer';
                    document.body.appendChild(link);
                    link.click();
                    document.body.removeChild(link);
                } else {
                    showToast('ArborGraph Setup completed successfully.');
                }
                // Reset back to Step 1
                wizardCurrentStep = 1;
                if (wizardEulaCheck) wizardEulaCheck.checked = false;
                if (wizardGateWarning) {
                    wizardGateWarning.classList.remove('accepted');
                    wizardGateWarning.innerHTML = '<svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="12" cy="12" r="10"></circle><line x1="12" y1="8" x2="12" y2="12"></line><line x1="12" y1="16" x2="12.01" y2="16"></line></svg><span>Acceptance required. The Next button is disabled until accepted.</span>';
                }
                updateWizardUI();
                return;
            }

            if (wizardCurrentStep < totalWizardSteps) {
                wizardCurrentStep++;
                updateWizardUI();
            }
        });
    }

    // Back button click
    if (btnWizardBack) {
        btnWizardBack.addEventListener('click', () => {
            if (wizardCurrentStep > 1 && wizardCurrentStep !== 6 && wizardCurrentStep !== 7) {
                wizardCurrentStep--;
                updateWizardUI();
            }
        });
    }

    // Cancel button click
    if (btnWizardCancel) {
        btnWizardCancel.addEventListener('click', () => {
            if (confirm('Cancel ArborGraph Setup?')) {
                wizardCurrentStep = 1;
                if (wizardEulaCheck) wizardEulaCheck.checked = false;
                updateWizardUI();
                showToast('Setup cancelled.');
            }
        });
    }

    // Allow clicking past completed steps or step 1
    wizardStepNodes.forEach(node => {
        node.addEventListener('click', () => {
            const targetStep = parseInt(node.getAttribute('data-step'), 10);
            if (targetStep < wizardCurrentStep || (targetStep === 2 && wizardCurrentStep === 1)) {
                wizardCurrentStep = targetStep;
                updateWizardUI();
            }
        });
    });

    // Destination directory toggle
    if (btnTogglePortablePath && wizardInstallPath) {
        let isDefaultProgFiles = true;
        btnTogglePortablePath.addEventListener('click', () => {
            if (isDefaultProgFiles) {
                wizardInstallPath.value = "%LocalAppData%\\ArborGraph";
                btnTogglePortablePath.textContent = "Use Program Files";
            } else {
                wizardInstallPath.value = "C:\\Program Files\\ArborGraph";
                btnTogglePortablePath.textContent = "Use Portable Dir";
            }
            isDefaultProgFiles = !isDefaultProgFiles;
            showToast(`Target path: ${wizardInstallPath.value}`);
        });
    }

    // Extraction simulation engine
    function startExtractionSimulation() {
        if (!wizardProgressFill || !wizardLogText || !wizardExtractStatus) return;

        wizardProgressFill.style.width = '0%';
        wizardExtractStatus.textContent = 'Extracting package files...';
        wizardLogText.textContent = '> Initializing Inno Setup 6.x engine...\n> Verifying NTFS write permissions...\n> Bounded staging buffer created.';

        const stages = [
            { pct: 18, msg: "Extracting core binary: ArborGraph.exe (73.4 MB)...", log: "> Extracting PE32+ executable header...\n> Unpacking bundled .NET 8.0 CLR runtime..." },
            { pct: 40, msg: "Deploying WPF presentation subsystem...", log: "> Registering PresentationCore & DirectX Hardware Acceleration pipeline..." },
            { pct: 62, msg: "Configuring SQLite database engine...", log: "> Unpacking Microsoft.Data.Sqlite & SQLitePCLRaw.bundle_e_sqlite3\n> Creating schema in %LocalAppData%\\ArborGraph..." },
            { pct: 82, msg: "Configuring application environment...", log: "> Setting WAL mode and 20,000-item channel capacity\n> Creating Start Menu & Desktop shortcuts..." },
            { pct: 96, msg: "Verifying cryptographic digest...", log: "> Validating SHA-256 binary digest: 31AC3B1D...40B0D85\n> Checksum verified bit-for-bit." },
            { pct: 100, msg: "Installation completed successfully.", log: "> All package files deployed.\n> Setup completed with exit code 0." }
        ];

        let index = 0;
        const interval = setInterval(() => {
            if (index < stages.length) {
                const s = stages[index];
                wizardProgressFill.style.width = `${s.pct}%`;
                wizardExtractStatus.textContent = s.msg;
                wizardLogText.textContent += `\n${s.log}`;
                wizardLogText.scrollTop = wizardLogText.scrollHeight;
                index++;
            } else {
                clearInterval(interval);
                setTimeout(() => {
                    wizardCurrentStep = 7;
                    updateWizardUI();
                }, 350);
            }
        }, 360);
    }

    function recordOfflineConsent() {
        const consentRecord = {
            eulaVersion: "1.0",
            accepted: true,
            acceptedAt: new Date().toISOString(),
            applicationVersion: "1.0.0",
            architecture: "win-x64",
            publisher: "AG DIAZ EVANGELISTA",
            offlineStorage: "%LocalAppData%\\ArborGraph"
        };

        try {
            localStorage.setItem('arborgraph_installer_consent', JSON.stringify(consentRecord));
        } catch (e) {
            // Local fallback
        }

        if (consentSummaryDetails) {
            consentSummaryDetails.innerHTML = `
                <div>EULA Version: ${consentRecord.eulaVersion} &bull; Status: Accepted (Offline Record Validated)</div>
                <div>Timestamp: ${consentRecord.acceptedAt}</div>
                <div>Storage: Saved locally in client storage (Zero Network Telemetry)</div>
            `;
        }
    }

    // =========================================================================
    // 6. DEMO UI 03: DISK DIAGNOSTICS & SCANNER SIMULATION
    // Replicating Views/ScannerView.xaml & Services/ScannerService.cs
    // =========================================================================
    const btnStartSim = document.getElementById('btnStartSim');
    const btnStopSim = document.getElementById('btnStopSim');
    const simBtnText = document.getElementById('simBtnText');
    const simDriveSelect = document.getElementById('simDriveSelect');
    const simPathInput = document.getElementById('simPathInput');
    const simTargetDesc = document.getElementById('simTargetDesc');
    const simProgressFill = document.getElementById('simProgressFill');
    const scanStateBadge = document.getElementById('scanStateBadge');

    const hudFiles = document.getElementById('hudFiles');
    const hudFolders = document.getElementById('hudFolders');
    const hudSpeed = document.getElementById('hudSpeed');
    const hudElapsed = document.getElementById('hudElapsed');
    const hudStorage = document.getElementById('hudStorage');
    const hudCleanable = document.getElementById('hudCleanable');
    const hudSkipped = document.getElementById('hudSkipped');
    const hudVisited = document.getElementById('hudVisited');
    const simCurrentPath = document.getElementById('simCurrentPath');
    const simDirectoryBuffer = document.getElementById('simDirectoryBuffer');

    const driveCapacityTitle = document.getElementById('driveCapacityTitle');
    const barUsed = document.getElementById('barUsed');
    const barJunk = document.getElementById('barJunk');
    const barFree = document.getElementById('barFree');
    const txtUsed = document.getElementById('txtUsed');
    const txtJunk = document.getElementById('txtJunk');
    const txtFree = document.getElementById('txtFree');

    const catVideo = document.getElementById('catVideo');
    const catPhotos = document.getElementById('catPhotos');
    const catPhotoshop = document.getElementById('catPhotoshop');
    const catNode = document.getElementById('catNode');
    const catCode = document.getElementById('catCode');

    const driveProfiles = {
        "C:": {
            target: "C:\\",
            title: "C:\\ Storage Allocation (512 GB NVMe SSD)",
            targetFiles: 148250,
            targetFolders: 14320,
            targetVolume: 52.4,
            targetJunk: 14.8,
            maxSpeed: 58400,
            usedPct: "74.6%",
            junkPct: "2.9%",
            freePct: "22.5%",
            usedText: "382.0 GB",
            junkText: "14.8 GB",
            freeText: "130.0 GB",
            cats: ["21.4 GB", "12.1 GB", "7.8 GB", "6.2 GB", "4.9 GB"]
        },
        "D:": {
            target: "D:\\",
            title: "D:\\ Storage Allocation (1.0 TB Developer NVMe)",
            targetFiles: 294100,
            targetFolders: 28940,
            targetVolume: 114.2,
            targetJunk: 28.6,
            maxSpeed: 64200,
            usedPct: "68.0%",
            junkPct: "2.9%",
            freePct: "29.1%",
            usedText: "680.0 GB",
            junkText: "28.6 GB",
            freeText: "320.0 GB",
            cats: ["14.2 GB", "18.5 GB", "12.4 GB", "28.6 GB", "42.8 GB"]
        },
        "E:": {
            target: "E:\\",
            title: "E:\\ Storage Allocation (4.0 TB Media Archive HDD)",
            targetFiles: 86400,
            targetFolders: 7210,
            targetVolume: 842.0,
            targetJunk: 4.2,
            maxSpeed: 42100,
            usedPct: "80.0%",
            junkPct: "0.1%",
            freePct: "19.9%",
            usedText: "3,200.0 GB",
            junkText: "4.2 GB",
            freeText: "800.0 GB",
            cats: ["412.0 GB", "248.0 GB", "116.0 GB", "4.2 GB", "8.5 GB"]
        }
    };

    const traversalPaths = [
        "C:\\Windows\\System32\\DriverStore\\FileRepository\\nv_dispi.inf_amd64",
        "C:\\Users\\AG\\AppData\\Local\\Temp\\scoped_dir_94812\\data.tmp",
        "C:\\Program Files\\Adobe\\Adobe Photoshop 2026\\Photoshop.exe",
        "C:\\Users\\AG\\.gradle\\caches\\modules-2\\files-2.1\\cache.bin",
        "C:\\Users\\AG\\source\\repos\\ArborGraph\\Services\\ScannerService.cs",
        "C:\\Users\\AG\\.cargo\\registry\\cache\\index.crates.io-6f17d22bba15001f",
        "C:\\Users\\AG\\AppData\\Local\\Microsoft\\Edge\\User Data\\Default\\Cache",
        "C:\\Users\\AG\\Videos\\Captures\\Master_Render_4K_ProRes.mov",
        "C:\\Users\\AG\\Documents\\Photoshop\\hero_keyvisual_master.psb",
        "C:\\Windows\\assembly\\NativeImages_v4.0.30319_64\\mscorlib.dll",
        "C:\\Users\\AG\\Projects\\web-client\\node_modules\\typescript\\lib\\tsc.js",
        "C:\\Users\\AG\\AppData\\Local\\npm-cache\\_cacache\\content-v2\\sha512",
        "C:\\Windows\\System32\\ntoskrnl.exe",
        "C:\\Program Files\\dotnet\\shared\\Microsoft.NETCore.App\\8.0.0\\coreclr.dll",
        "C:\\Users\\AG\\Projects\\ArborGraph\\Controls\\RealtimeMetricGraph.cs"
    ];

    function updateDriveDisplay(driveKey) {
        const p = driveProfiles[driveKey] || driveProfiles["C:"];
        if (simTargetDesc) simTargetDesc.textContent = p.target;
        if (simPathInput) simPathInput.value = p.target;
        if (driveCapacityTitle) driveCapacityTitle.textContent = p.title;

        if (barUsed) barUsed.style.width = p.usedPct;
        if (barJunk) barJunk.style.width = p.junkPct;
        if (barFree) barFree.style.width = p.freePct;

        if (txtUsed) txtUsed.textContent = p.usedText;
        if (txtJunk) txtJunk.textContent = p.junkText;
        if (txtFree) txtFree.textContent = p.freeText;

        if (catVideo) catVideo.textContent = p.cats[0];
        if (catPhotos) catPhotos.textContent = p.cats[1];
        if (catPhotoshop) catPhotoshop.textContent = p.cats[2];
        if (catNode) catNode.textContent = p.cats[3];
        if (catCode) catCode.textContent = p.cats[4];
    }

    if (simDriveSelect) {
        simDriveSelect.addEventListener('change', () => {
            updateDriveDisplay(simDriveSelect.value);
            if (!isCurrentlyScanning) {
                if (simCurrentPath) simCurrentPath.textContent = `Target set to ${simDriveSelect.value}\\. Ready for scan execution.`;
            }
        });
    }

    let scanTimer = null;
    let scanStartTime = 0;
    const SCAN_DURATION_MS = 2800; // Simulated speed traversal

    function startScanner() {
        if (isCurrentlyScanning) return;
        isCurrentlyScanning = true;

        const driveVal = simDriveSelect ? simDriveSelect.value : "C:";
        const profile = driveProfiles[driveVal] || driveProfiles["C:"];

        // Update UI controls
        if (btnStartSim) btnStartSim.style.display = 'none';
        if (btnStopSim) btnStopSim.style.display = 'inline-flex';
        if (scanStateBadge) {
            scanStateBadge.textContent = "Scanning...";
            scanStateBadge.style.color = "var(--color-accent)";
        }

        scanStartTime = performance.now();
        let pathIndex = 0;
        let visitedCount = 0;

        // Clear buffer
        if (simDirectoryBuffer) {
            simDirectoryBuffer.innerHTML = '<div class="buffer-row" style="color: var(--color-accent); font-weight: 600;">[Scanner Service] Traversal initiated across ' + driveVal + '\\ (8 threads)</div>';
        }

        scanTimer = setInterval(() => {
            const elapsed = performance.now() - scanStartTime;
            const progress = Math.min(1, elapsed / SCAN_DURATION_MS);

            // Progress bar
            if (simProgressFill) simProgressFill.style.width = `${(progress * 100).toFixed(1)}%`;

            // Plain language primary metrics
            const currentFiles = Math.floor(progress * profile.targetFiles);
            const currentFolders = Math.floor(progress * profile.targetFolders);
            const currentSpeed = Math.floor(profile.maxSpeed * (0.85 + Math.random() * 0.15));

            if (hudFiles) hudFiles.textContent = currentFiles.toLocaleString();
            if (hudFolders) hudFolders.textContent = currentFolders.toLocaleString();
            if (hudSpeed) hudSpeed.textContent = `${currentSpeed.toLocaleString()} f/s`;

            // Elapsed time formatted MM:SS.S
            const totalSec = elapsed / 1000;
            const mins = Math.floor(totalSec / 60).toString().padStart(2, '0');
            const secs = (totalSec % 60).toFixed(1).padStart(4, '0');
            if (hudElapsed) hudElapsed.textContent = `${mins}:${secs}`;

            // Secondary metrics
            if (hudStorage) hudStorage.textContent = `${(progress * profile.targetVolume).toFixed(1)} GB`;
            if (hudCleanable) hudCleanable.textContent = `${(progress * profile.targetJunk).toFixed(1)} GB`;
            if (hudSkipped) hudSkipped.textContent = `${Math.floor(progress * 14)} files`;

            // Visited & Active Path
            visitedCount += Math.floor(currentSpeed / 20);
            if (hudVisited) hudVisited.textContent = `Visited: ${visitedCount.toLocaleString()}`;

            pathIndex = (pathIndex + 1) % traversalPaths.length;
            const currentSample = traversalPaths[pathIndex];
            if (simCurrentPath) simCurrentPath.textContent = currentSample;

            // Rolling directory feed (top-insertion with max 25 rows)
            if (simDirectoryBuffer) {
                const row = document.createElement('div');
                row.className = 'buffer-row';
                row.textContent = currentSample;
                simDirectoryBuffer.insertBefore(row, simDirectoryBuffer.firstChild);

                if (simDirectoryBuffer.children.length > 25) {
                    simDirectoryBuffer.removeChild(simDirectoryBuffer.lastChild);
                }
            }

            // Completion
            if (progress >= 1) {
                stopScanner(true);
            }
        }, 60);
    }

    function stopScanner(isComplete) {
        clearInterval(scanTimer);
        isCurrentlyScanning = false;

        const driveVal = simDriveSelect ? simDriveSelect.value : "C:";
        const profile = driveProfiles[driveVal] || driveProfiles["C:"];

        if (btnStartSim) {
            btnStartSim.style.display = 'inline-flex';
            if (simBtnText) simBtnText.textContent = isComplete ? "Scan Again" : "Resume Scan";
        }
        if (btnStopSim) btnStopSim.style.display = 'none';

        if (scanStateBadge) {
            scanStateBadge.textContent = isComplete ? "Completed" : "Stopped";
            scanStateBadge.style.color = isComplete ? "var(--color-success)" : "var(--color-danger)";
        }

        if (hudSpeed) hudSpeed.textContent = "0 f/s";

        if (isComplete) {
            if (simProgressFill) simProgressFill.style.width = '100%';
            if (hudFiles) hudFiles.textContent = profile.targetFiles.toLocaleString();
            if (hudFolders) hudFolders.textContent = profile.targetFolders.toLocaleString();
            if (hudStorage) hudStorage.textContent = `${profile.targetVolume.toFixed(1)} GB`;
            if (hudCleanable) hudCleanable.textContent = `${profile.targetJunk.toFixed(1)} GB`;
            if (simCurrentPath) simCurrentPath.textContent = `Traversal complete: ${profile.targetFiles.toLocaleString()} files indexed into SQLite database.`;

            if (simDirectoryBuffer) {
                const row = document.createElement('div');
                row.className = 'buffer-row';
                row.style.color = 'var(--color-success)';
                row.style.fontWeight = '600';
                row.textContent = `[Index Engine] SQLite WAL commit complete. Secondary b-trees optimized in 38ms.`;
                simDirectoryBuffer.insertBefore(row, simDirectoryBuffer.firstChild);
            }
            showToast(`Filesystem scan of ${driveVal} completed successfully!`);
        } else {
            if (simCurrentPath) simCurrentPath.textContent = `Scan suspended by user. SQLite journal checkpointed.`;
            showToast('Scan stopped by user.');
        }
    }

    if (btnStartSim) btnStartSim.addEventListener('click', startScanner);
    if (btnStopSim) btnStopSim.addEventListener('click', () => stopScanner(false));

    // Export report demo
    const btnExportSim = document.getElementById('btnExportSim');
    if (btnExportSim) {
        btnExportSim.addEventListener('click', () => {
            showToast('Diagnostic summary exported to clipboard (JSON format).');
        });
    }

    // =========================================================================
    // 7. INTERACTIVE SQUARIFIED TREEMAP WITH BREADCRUMB DRILLDOWN
    // Translating Infrastructure/TreemapLayoutEngine.cs
    // Hierarchy: C:\ -> Users -> AG -> Projects
    // =========================================================================
    const treemapCanvas = document.getElementById('treemapCanvas');
    const treemapBreadcrumbs = document.getElementById('treemapBreadcrumbs');

    // Category slate colors matching TreemapLayoutEngine.cs
    // Folder: #1F2937, Video: #64748B, Photos: #475569, Executables: #374151,
    // Archives: #6B7280, Code: #334155, Cleanable: #06B6D4
    const treemapHierarchy = {
        id: "root",
        name: "C:\\",
        size: "382.0 GB",
        children: [
            {
                id: "users",
                name: "Users",
                size: "248.5 GB",
                weight: 65,
                category: "Folder",
                color: "#1F2937",
                children: [
                    {
                        id: "ag",
                        name: "AG",
                        size: "214.2 GB",
                        weight: 86,
                        category: "User Directory",
                        color: "#1F2937",
                        children: [
                            {
                                id: "projects",
                                name: "Projects",
                                size: "124.6 GB",
                                weight: 58,
                                category: "Source Workspace",
                                color: "#1F2937",
                                children: [
                                    { id: "arborgraph", name: "ArborGraph", size: "38.4 GB", weight: 31, category: "C# / WPF (.NET 8)", color: "#334155" },
                                    { id: "nodemodules", name: "node_modules", size: "32.6 GB", weight: 26, category: "Cleanable Cache", color: "#06B6D4" },
                                    { id: "unreal", name: "UnrealEngine", size: "29.1 GB", weight: 23, category: "3D / Shaders", color: "#475569" },
                                    { id: "photoshop", name: "PhotoshopPSD", size: "24.5 GB", weight: 20, category: "RAW & PSD", color: "#64748B" }
                                ]
                            },
                            { id: "appdata", name: "AppData", size: "48.2 GB", weight: 22, category: "Local Cache", color: "#374151" },
                            { id: "downloads", name: "Downloads", size: "26.4 GB", weight: 12, category: "Archives", color: "#6B7280" },
                            { id: "videos", name: "Videos", size: "15.0 GB", weight: 8, category: "Rendered Captures", color: "#64748B" }
                        ]
                    },
                    { id: "public", name: "Public", size: "33.1 GB", weight: 13, category: "Shared", color: "#475569" },
                    { id: "defaultuser", name: "Default", size: "1.2 GB", weight: 1, category: "System", color: "#374151" }
                ]
            },
            {
                id: "progfiles",
                name: "Program Files",
                size: "52.1 GB",
                weight: 14,
                category: "Applications",
                color: "#374151",
                children: [
                    { id: "adobe", name: "Adobe", size: "28.4 GB", weight: 55, category: "Design Apps", color: "#374151" },
                    { id: "dotnet", name: "dotnet", size: "14.2 GB", weight: 27, category: "Runtimes", color: "#334155" },
                    { id: "git", name: "Git", size: "9.5 GB", weight: 18, category: "Tools", color: "#4B5563" }
                ]
            },
            {
                id: "windows",
                name: "Windows",
                size: "38.2 GB",
                weight: 10,
                category: "System OS",
                color: "#334155",
                children: [
                    { id: "sys32", name: "System32", size: "18.4 GB", weight: 48, category: "Binaries", color: "#334155" },
                    { id: "winsxs", name: "WinSxS", size: "14.2 GB", weight: 37, category: "Side-by-Side Assemblies", color: "#475569" },
                    { id: "sysother", name: "Other", size: "5.6 GB", weight: 15, category: "System", color: "#374151" }
                ]
            },
            {
                id: "pagefile",
                name: "pagefile.sys",
                size: "16.0 GB",
                weight: 4,
                category: "Virtual Memory",
                color: "#475569"
            },
            {
                id: "cleanable",
                name: "Temp & Caches",
                size: "14.8 GB",
                weight: 4,
                category: "Cleanable Cache",
                color: "#06B6D4"
            },
            {
                id: "hiberfil",
                name: "hiberfil.sys",
                size: "12.4 GB",
                weight: 3,
                category: "Hibernation File",
                color: "#6B7280"
            }
        ]
    };

    let breadcrumbTrail = [treemapHierarchy];

    // Squarified 2D partitioner (calculates left%, top%, width%, height% for a list of items)
    function layoutTreemapTiles(items, x, y, w, h) {
        if (!items || items.length === 0) return [];
        if (items.length === 1) {
            return [{ item: items[0], x, y, w, h }];
        }

        const totalWeight = items.reduce((acc, it) => acc + (it.weight || 1), 0);
        const results = [];

        // Split items into two balanced halves
        let halfWeight = 0;
        let splitIdx = 0;
        for (let i = 0; i < items.length - 1; i++) {
            halfWeight += (items[i].weight || 1);
            if (halfWeight >= totalWeight / 2) {
                splitIdx = i + 1;
                break;
            }
        }
        if (splitIdx === 0) splitIdx = 1;

        const groupA = items.slice(0, splitIdx);
        const groupB = items.slice(splitIdx);
        const weightA = groupA.reduce((acc, it) => acc + (it.weight || 1), 0);
        const ratioA = weightA / totalWeight;

        if (w >= h) {
            // Horizontal split
            const wA = w * ratioA;
            const wB = w - wA;
            results.push(...layoutTreemapTiles(groupA, x, y, wA, h));
            results.push(...layoutTreemapTiles(groupB, x + wA, y, wB, h));
        } else {
            // Vertical split
            const hA = h * ratioA;
            const hB = h - hA;
            results.push(...layoutTreemapTiles(groupA, x, y, w, hA));
            results.push(...layoutTreemapTiles(groupB, x, y + hA, w, hB));
        }

        return results;
    }

    function renderTreemap() {
        if (!treemapCanvas) return;
        treemapCanvas.innerHTML = '';

        const currentNode = breadcrumbTrail[breadcrumbTrail.length - 1];
        const children = currentNode.children || [];

        // Update Breadcrumbs
        if (treemapBreadcrumbs) {
            treemapBreadcrumbs.innerHTML = '';
            breadcrumbTrail.forEach((node, idx) => {
                const isLast = idx === breadcrumbTrail.length - 1;
                const span = document.createElement('span');
                span.className = isLast ? 'crumb-item current' : 'crumb-item';
                span.textContent = node.name;
                span.setAttribute('data-index', idx);

                if (!isLast) {
                    span.addEventListener('click', () => {
                        breadcrumbTrail = breadcrumbTrail.slice(0, idx + 1);
                        renderTreemap();
                    });
                }

                treemapBreadcrumbs.appendChild(span);

                if (!isLast) {
                    const sep = document.createElement('span');
                    sep.innerHTML = '&rsaquo;';
                    sep.style.color = 'var(--color-text-muted)';
                    treemapBreadcrumbs.appendChild(sep);
                }
            });
        }

        if (children.length === 0) {
            const emptyNotice = document.createElement('div');
            emptyNotice.style.padding = '20px';
            emptyNotice.style.color = '#FFFFFF';
            emptyNotice.style.fontSize = '12px';
            emptyNotice.textContent = `Terminal node: ${currentNode.name} (${currentNode.size})`;
            treemapCanvas.appendChild(emptyNotice);
            return;
        }

        // Layout tiles
        const tiles = layoutTreemapTiles(children, 0, 0, 100, 100);

        tiles.forEach(({ item, x, y, w, h }) => {
            const tileEl = document.createElement('div');
            tileEl.className = 'tm-tile';
            tileEl.style.left = `${x}%`;
            tileEl.style.top = `${y}%`;
            tileEl.style.width = `${w}%`;
            tileEl.style.height = `${h}%`;
            tileEl.style.backgroundColor = item.color || '#334155';
            tileEl.title = `${item.name}\nSize: ${item.size}\nCategory: ${item.category || 'File'}${item.children ? ' (Click to inspect)' : ''}`;

            const title = document.createElement('div');
            title.className = 'tm-title';
            title.textContent = item.name;

            const size = document.createElement('div');
            size.className = 'tm-size';
            size.textContent = item.size;

            tileEl.appendChild(title);
            if (h > 18 && w > 12) {
                tileEl.appendChild(size);
            }

            // Click to drill down if node has children
            if (item.children && item.children.length > 0) {
                tileEl.style.cursor = 'pointer';
                tileEl.addEventListener('click', () => {
                    breadcrumbTrail.push(item);
                    renderTreemap();
                });
            } else {
                tileEl.addEventListener('click', () => {
                    showToast(`${item.name}: ${item.size} (${item.category || 'Item'})`);
                });
            }

            treemapCanvas.appendChild(tileEl);
        });
    }

    // Initialize treemap at root
    renderTreemap();

    // =========================================================================
    // 8. DEMO UI 04: PACKAGE INTEGRITY VERIFICATION
    // Cryptographic hash validation & Windows PowerShell utility
    // =========================================================================
    const copyOfficialHashBtn = document.getElementById('copyOfficialHashBtn');
    const verifyInput = document.getElementById('verifyInput');
    const verifyBtn = document.getElementById('verifyBtn');
    const verifyResult = document.getElementById('verifyResult');

    if (copyOfficialHashBtn) {
        copyOfficialHashBtn.addEventListener('click', () => {
            navigator.clipboard.writeText(OFFICIAL_HASH).then(() => {
                showToast('Official SHA-256 digest copied to clipboard!');
            }).catch(() => {
                showToast(`Hash: ${OFFICIAL_HASH}`);
            });
        });
    }

    if (verifyBtn && verifyInput && verifyResult) {
        verifyBtn.addEventListener('click', () => {
            const entered = verifyInput.value.trim().toUpperCase();
            if (!entered) {
                showToast('Please enter or paste a SHA-256 hash first.');
                return;
            }

            verifyResult.style.display = 'block';
            if (entered === OFFICIAL_HASH) {
                verifyResult.className = 'verify-status-banner match';
                verifyResult.innerHTML = `
                    <div style="font-weight: 600; color: var(--color-success); font-size: 12.5px;">✓ INTEGRITY VERIFIED: Cryptographic Match Confirmed</div>
                    <div style="font-size: 11px; color: var(--color-text-secondary); margin-top: 4px;">The computed digest matches the official release binary bit-for-bit. Package is genuine and uncorrupted.</div>
                `;
                showToast('Integrity confirmed: Authenticated Release.');
            } else {
                verifyResult.className = 'verify-status-banner mismatch';
                verifyResult.innerHTML = `
                    <div style="font-weight: 600; color: var(--color-danger); font-size: 12.5px;">✕ CHECKSUM MISMATCH DETECTED</div>
                    <div style="font-size: 11px; color: var(--color-text-secondary); margin-top: 4px;">Entered hash does not match official digest. Expected: ${OFFICIAL_HASH.substring(0, 16)}...</div>
                `;
                showToast('Hash mismatch: Binary may be corrupted or modified.');
            }
        });
    }

    // =========================================================================
    // 9. DOCUMENTATION SUBNAVIGATION (8 Articles)
    // =========================================================================
    const docButtons = document.querySelectorAll('.subnav-btn[data-target-doc]');
    const docSections = document.querySelectorAll('#docReadingPane .doc-section');

    function switchDoc(targetId) {
        docButtons.forEach(btn => {
            btn.classList.toggle('active', btn.getAttribute('data-target-doc') === targetId);
        });

        docSections.forEach(sec => {
            sec.classList.toggle('active', sec.id === targetId);
        });
    }

    docButtons.forEach(btn => {
        btn.addEventListener('click', () => {
            const targetId = btn.getAttribute('data-target-doc');
            if (targetId) switchDoc(targetId);
        });
    });

    // =========================================================================
    // 10. LEGAL & COMPLIANCE SUBNAVIGATION (4 Policies)
    // =========================================================================
    const legalButtons = document.querySelectorAll('.subnav-btn[data-target-legal]');
    const legalSections = document.querySelectorAll('#legalReadingPane .doc-section');

    function switchLegal(targetId) {
        legalButtons.forEach(btn => {
            btn.classList.toggle('active', btn.getAttribute('data-target-legal') === targetId);
        });

        legalSections.forEach(sec => {
            sec.classList.toggle('active', sec.id === targetId);
        });
    }

    legalButtons.forEach(btn => {
        btn.addEventListener('click', () => {
            const targetId = btn.getAttribute('data-target-legal');
            if (targetId) switchLegal(targetId);
        });
    });

    // =========================================================================
    // 11. FOOTER & TOC DIRECT LINK DISPATCHERS
    // =========================================================================
    document.querySelectorAll('.footer-doc-link').forEach(link => {
        link.addEventListener('click', (e) => {
            e.preventDefault();
            const targetDoc = link.getAttribute('data-doc');
            if (targetDoc) switchDoc(targetDoc);
            const docElement = document.getElementById('docs');
            if (docElement) docElement.scrollIntoView({ behavior: 'smooth' });
        });
    });

    document.querySelectorAll('.footer-legal-link').forEach(link => {
        link.addEventListener('click', (e) => {
            e.preventDefault();
            const targetLegal = link.getAttribute('data-legal');
            if (targetLegal) switchLegal(targetLegal);
            const legalElement = document.getElementById('legal');
            if (legalElement) legalElement.scrollIntoView({ behavior: 'smooth' });
        });
    });

    // =========================================================================
    // 12. GLOBAL COPY BUTTONS
    // =========================================================================
    document.querySelectorAll('.copy-hash-btn').forEach(btn => {
        btn.addEventListener('click', () => {
            const hash = btn.getAttribute('data-hash') || OFFICIAL_HASH;
            navigator.clipboard.writeText(hash).then(() => {
                showToast('SHA-256 checksum copied to clipboard!');
            }).catch(() => {
                showToast(`Hash: ${hash}`);
            });
        });
    });

    document.querySelectorAll('.copy-cmd-btn').forEach(btn => {
        btn.addEventListener('click', () => {
            const targetId = btn.getAttribute('data-target');
            const targetEl = document.getElementById(targetId);
            if (targetEl) {
                const text = targetEl.textContent.trim();
                navigator.clipboard.writeText(text).then(() => {
                    showToast('Command copied to clipboard!');
                }).catch(() => {
                    showToast(`Copied: ${text}`);
                });
            }
        });
    });

    // Initial URL Hash Support
    if (window.location.hash) {
        const hash = window.location.hash.replace('#', '');
        if (hash.startsWith('doc')) {
            switchDoc(hash);
            const docsEl = document.getElementById('docs');
            if (docsEl) docsEl.scrollIntoView();
        } else if (hash.startsWith('legal')) {
            switchLegal(hash);
            const legalEl = document.getElementById('legal');
            if (legalEl) legalEl.scrollIntoView();
        }
    }

    // Initialize Setup Stepper UI
    updateWizardUI();
    updateDriveDisplay("C:");
});

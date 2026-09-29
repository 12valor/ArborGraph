// DiskScope Pro: Windows Disk Analysis & Setup Utility Script
document.addEventListener('DOMContentLoaded', () => {
    const OFFICIAL_HASH = '095EAE7AFB4AC3AC15F504EC998B839C99032BDC0A46CBD1239309C39C8FCB4C';

    // =========================================================
    // 1. APPLICATION TAB NAVIGATION & DEEP LINKING
    // =========================================================
    const tabButtons = document.querySelectorAll('.win-tab');
    const tabPanes = document.querySelectorAll('.tab-pane');

    function switchTab(tabId) {
        tabButtons.forEach(btn => {
            const matches = btn.getAttribute('data-tab') === tabId;
            btn.classList.toggle('active', matches);
            btn.setAttribute('aria-selected', matches ? 'true' : 'false');
        });

        tabPanes.forEach(pane => {
            const matches = pane.id === `pane${tabId.charAt(0).toUpperCase() + tabId.slice(1)}`;
            pane.classList.toggle('active', matches);
        });
    }

    tabButtons.forEach(btn => {
        btn.addEventListener('click', () => {
            const tabId = btn.getAttribute('data-tab');
            if (tabId) {
                switchTab(tabId);
                history.replaceState(null, '', `#${tabId}`);
            }
        });
    });

    // =========================================================
    // 2. TOAST NOTIFICATION HELPER
    // =========================================================
    const toast = document.getElementById('toastMsg');
    let toastTimeout;
    function showToast(message) {
        if (!toast) return;
        toast.textContent = message;
        toast.classList.add('show');
        clearTimeout(toastTimeout);
        toastTimeout = setTimeout(() => {
            toast.classList.remove('show');
        }, 3200);
    }

    // =========================================================
    // 3. CLIENT OS & ARCHITECTURE DIAGNOSTICS
    // =========================================================
    const systemDiagStatus = document.getElementById('systemDiagStatus');
    if (systemDiagStatus) {
        const userAgent = window.navigator.userAgent;
        let osText = 'SYSTEM READY: Windows 64-bit Architecture Verified';
        if (/Win64|x64|WOW64/i.test(userAgent)) {
            osText = 'SYSTEM READY: Windows 64-bit Architecture Verified';
        } else if (/Windows/i.test(userAgent)) {
            osText = 'SYSTEM READY: Windows OS Architecture Detected';
        } else if (/Mac/i.test(userAgent)) {
            osText = 'NOTICE: macOS Detected. DiskScope runs natively on Windows 10/11 x64.';
        } else if (/Linux/i.test(userAgent)) {
            osText = 'NOTICE: Linux Detected. DiskScope runs natively on Windows 10/11 x64.';
        }
        systemDiagStatus.textContent = osText;
    }

    // =========================================================
    // 4. WINDOWS SETUP WIZARD WITH STRICT EULA CONSENT GATE
    // =========================================================
    let wizardCurrentStep = 1;
    const totalWizardSteps = 7;
    const stepNames = [
        "Welcome",
        "License Agreement",
        "Installation Location",
        "Installation Options",
        "Ready to Install",
        "Installation Progress",
        "Complete"
    ];

    const wizardStepBadge = document.getElementById('wizardStepBadge');
    const wizardStepItems = document.querySelectorAll('.wizard-step-item');
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
    const wizardLogText = document.getElementById('wizardLogText');
    const wizardExtractStatus = document.getElementById('wizardExtractStatus');
    const consentSummaryDetails = document.getElementById('consentSummaryDetails');
    const wizardLaunchApp = document.getElementById('wizardLaunchApp');

    function updateWizardUI() {
        // 1. Update step badge
        if (wizardStepBadge) {
            wizardStepBadge.textContent = `Step ${wizardCurrentStep} of ${totalWizardSteps}: ${stepNames[wizardCurrentStep - 1]}`;
        }

        // 2. Update step sidebar list
        wizardStepItems.forEach(item => {
            const stepNum = parseInt(item.getAttribute('data-step'), 10);
            item.classList.toggle('active', stepNum === wizardCurrentStep);
            item.classList.toggle('completed', stepNum < wizardCurrentStep);
        });

        // 3. Switch active pane
        wizardPanes.forEach(pane => {
            const isTarget = pane.id === `wizardStep${wizardCurrentStep}`;
            pane.classList.toggle('active', isTarget);
        });

        // 4. Update Back button state
        if (btnWizardBack) {
            btnWizardBack.disabled = (wizardCurrentStep === 1 || wizardCurrentStep === 6 || wizardCurrentStep === 7);
        }

        // 5. Update Next/Install/Finish button state and text
        if (btnWizardNext) {
            if (wizardCurrentStep === 2) {
                // EULA CONSENT GATE: NEXT IS STRICTLY DISABLED UNLESS CHECKED
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

        // 6. Step 5 summary update
        if (wizardCurrentStep === 5 && summaryLocation && wizardInstallPath) {
            summaryLocation.textContent = wizardInstallPath.value;
        }

        // 7. Step 6 extraction trigger
        if (wizardCurrentStep === 6) {
            startExtractionSimulation();
        }

        // 8. Step 7 complete & local consent record
        if (wizardCurrentStep === 7) {
            recordOfflineConsent();
        }
    }

    // EULA Consent Checkbox Listener
    if (wizardEulaCheck) {
        wizardEulaCheck.addEventListener('change', () => {
            const isAccepted = wizardEulaCheck.checked;
            if (wizardCurrentStep === 2 && btnWizardNext) {
                btnWizardNext.disabled = !isAccepted;
            }
            if (wizardGateWarning) {
                if (isAccepted) {
                    wizardGateWarning.classList.add('accepted');
                    wizardGateWarning.innerHTML = `
                        <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5"><polyline points="20 6 9 17 4 12"></polyline></svg>
                        <span>Agreement accepted. You may now continue.</span>
                    `;
                } else {
                    wizardGateWarning.classList.remove('accepted');
                    wizardGateWarning.innerHTML = `
                        <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="12" cy="12" r="10"></circle><line x1="12" y1="8" x2="12" y2="12"></line><line x1="12" y1="16" x2="12.01" y2="16"></line></svg>
                        <span>Acceptance required. The Next button is disabled until accepted.</span>
                    `;
                }
            }
        });
    }

    // Next / Install / Finish button listener
    if (btnWizardNext) {
        btnWizardNext.addEventListener('click', () => {
            // Guard: Cannot advance past step 2 without explicit consent
            if (wizardCurrentStep === 2 && !wizardEulaCheck.checked) {
                showToast('Please check the agreement box to accept the EULA.');
                return;
            }

            if (wizardCurrentStep === 7) {
                // Finish button clicked
                if (wizardLaunchApp && wizardLaunchApp.checked) {
                    showToast('Launching DiskScope Pro (Downloading binary)...');
                    const link = document.createElement('a');
                    link.href = 'downloads/DiskScope.exe';
                    link.download = 'DiskScope.exe';
                    document.body.appendChild(link);
                    link.click();
                    document.body.removeChild(link);
                } else {
                    showToast('DiskScope Pro Setup successfully completed.');
                }
                // Reset wizard back to Step 1
                wizardCurrentStep = 1;
                if (wizardEulaCheck) wizardEulaCheck.checked = false;
                if (wizardGateWarning) {
                    wizardGateWarning.classList.remove('accepted');
                    wizardGateWarning.innerHTML = `
                        <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="12" cy="12" r="10"></circle><line x1="12" y1="8" x2="12" y2="12"></line><line x1="12" y1="16" x2="12.01" y2="16"></line></svg>
                        <span>Acceptance required. The Next button is disabled until accepted.</span>
                    `;
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

    // Back button listener
    if (btnWizardBack) {
        btnWizardBack.addEventListener('click', () => {
            if (wizardCurrentStep > 1 && wizardCurrentStep !== 6 && wizardCurrentStep !== 7) {
                wizardCurrentStep--;
                updateWizardUI();
            }
        });
    }

    // Cancel button listener
    if (btnWizardCancel) {
        btnWizardCancel.addEventListener('click', () => {
            if (confirm('Are you sure you want to cancel DiskScope Pro Setup?')) {
                wizardCurrentStep = 1;
                if (wizardEulaCheck) wizardEulaCheck.checked = false;
                updateWizardUI();
                showToast('Setup cancelled.');
            }
        });
    }

    // Destination Path Toggle
    if (btnTogglePortablePath && wizardInstallPath) {
        let isDefaultProgFiles = true;
        btnTogglePortablePath.addEventListener('click', () => {
            if (isDefaultProgFiles) {
                wizardInstallPath.value = "%LocalAppData%\\DiskScopePro";
                btnTogglePortablePath.textContent = "Use Program Files";
            } else {
                wizardInstallPath.value = "C:\\Program Files\\DiskScope";
                btnTogglePortablePath.textContent = "Use Portable Dir";
            }
            isDefaultProgFiles = !isDefaultProgFiles;
            showToast(`Installation path updated: ${wizardInstallPath.value}`);
        });
    }

    // Simulated Extraction Engine for Step 6
    function startExtractionSimulation() {
        if (!wizardProgressFill || !wizardLogText || !wizardExtractStatus) return;

        wizardProgressFill.style.width = '0%';
        wizardExtractStatus.textContent = 'Extracting DiskScope.exe package...';
        wizardLogText.textContent = '> Initializing Windows Installer engine...\n> Verifying local NTFS volume permissions...\n> Bounded staging buffer created.';

        const steps = [
            { pct: 15, msg: "Extracting core binary: DiskScope.exe (72.7 MB)...", log: "> Extracting PE32+ executable header...\n> Unpacking bundled .NET 8.0 runtime assemblies..." },
            { pct: 35, msg: "Deploying WPF presentation subsystem...", log: "> Registering PresentationCore.dll & PresentationFramework.dll\n> Validating DirectX Hardware Acceleration..." },
            { pct: 58, msg: "Configuring SQLite database subsystem...", log: "> Unpacking Microsoft.Data.Sqlite & SQLitePCLRaw.bundle_e_sqlite3\n> Registering local database schema in %LocalAppData%\\DiskScopePro..." },
            { pct: 78, msg: "Creating application environment...", log: "> Configuring WAL journal mode and 20,000-item channel capacity\n> Creating Start Menu & Desktop shortcuts..." },
            { pct: 95, msg: "Verifying package cryptographic checksum...", log: "> Validating SHA-256 binary digest: 095EAE7A...FCB4C\n> Cryptographic match verified bit-for-bit." },
            { pct: 100, msg: "Setup installation completed successfully.", log: "> All package files deployed.\n> Setup completed with exit code 0." }
        ];

        let index = 0;
        const interval = setInterval(() => {
            if (index < steps.length) {
                const s = steps[index];
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
                }, 400);
            }
        }, 380);
    }

    // Offline Local Consent Recorder (Zero Network Telemetry)
    function recordOfflineConsent() {
        const consentRecord = {
            eulaVersion: "1.0",
            accepted: true,
            acceptedAt: new Date().toISOString(),
            applicationVersion: "1.0.0",
            architecture: "win-x64",
            publisher: "[OWNER / PUBLISHER NAME]",
            offlineStorage: "%LocalAppData%\\DiskScopePro"
        };

        try {
            localStorage.setItem('diskscope_installer_consent', JSON.stringify(consentRecord));
        } catch (e) {
            // LocalStorage fallback for file:// protocol if restricted
        }

        if (consentSummaryDetails) {
            consentSummaryDetails.innerHTML = `
                <div>EULA Version: ${consentRecord.eulaVersion}</div>
                <div>Status: Accepted (Offline Record Validated)</div>
                <div>Timestamp: ${consentRecord.acceptedAt}</div>
                <div>Storage: Stored exclusively in local client storage</div>
            `;
        }
    }

    // =========================================================
    // 5. DOCUMENTATION SUBNAVIGATION
    // =========================================================
    const docButtons = document.querySelectorAll('.subnav-btn[data-target-doc]');
    const docSections = document.querySelectorAll('#docReadingPane .doc-section');

    function switchDoc(targetId) {
        docButtons.forEach(btn => {
            const matches = btn.getAttribute('data-target-doc') === targetId;
            btn.classList.toggle('active', matches);
        });

        docSections.forEach(sec => {
            const matches = sec.id === targetId;
            sec.classList.toggle('active', matches);
        });
    }

    docButtons.forEach(btn => {
        btn.addEventListener('click', () => {
            const targetId = btn.getAttribute('data-target-doc');
            if (targetId) switchDoc(targetId);
        });
    });

    // =========================================================
    // 6. LEGAL & COMPLIANCE SUBNAVIGATION
    // =========================================================
    const legalButtons = document.querySelectorAll('.subnav-btn[data-target-legal]');
    const legalSections = document.querySelectorAll('#legalReadingPane .doc-section');

    function switchLegal(targetId) {
        legalButtons.forEach(btn => {
            const matches = btn.getAttribute('data-target-legal') === targetId;
            btn.classList.toggle('active', matches);
        });

        legalSections.forEach(sec => {
            const matches = sec.id === targetId;
            sec.classList.toggle('active', matches);
        });
    }

    legalButtons.forEach(btn => {
        btn.addEventListener('click', () => {
            const targetId = btn.getAttribute('data-target-legal');
            if (targetId) switchLegal(targetId);
        });
    });

    // =========================================================
    // 7. FOOTER DIRECT LINK DISPATCHER
    // =========================================================
    const footerDocLinks = document.querySelectorAll('.footer-doc-link');
    footerDocLinks.forEach(link => {
        link.addEventListener('click', (e) => {
            e.preventDefault();
            switchTab('docs');
            const targetDoc = link.getAttribute('data-doc');
            if (targetDoc) switchDoc(targetDoc);
            window.scrollTo({ top: 120, behavior: 'smooth' });
        });
    });

    const footerLegalLinks = document.querySelectorAll('.footer-legal-link');
    footerLegalLinks.forEach(link => {
        link.addEventListener('click', (e) => {
            e.preventDefault();
            switchTab('legal');
            const targetLegal = link.getAttribute('data-legal');
            if (targetLegal) switchLegal(targetLegal);
            window.scrollTo({ top: 120, behavior: 'smooth' });
        });
    });

    // Initial URL Hash Parsing with Sub-route support
    if (window.location.hash) {
        const hash = window.location.hash.replace('#', '').toLowerCase();
        const validTabs = ['setup', 'scanner', 'verify', 'specs', 'docs', 'legal', 'changelog'];
        if (validTabs.includes(hash)) {
            switchTab(hash);
        } else if (hash.startsWith('docs-') || hash.startsWith('doc')) {
            switchTab('docs');
            const matchingDoc = document.getElementById(hash);
            if (matchingDoc) switchDoc(hash);
        } else if (hash.startsWith('legal-') || hash.startsWith('legal')) {
            switchTab('legal');
            const matchingLegal = document.getElementById(hash);
            if (matchingLegal) switchLegal(hash);
        }
    }

    // =========================================================
    // 8. DISK DIAGNOSTICS & SCANNER SIMULATOR
    // =========================================================
    const btnStartSim = document.getElementById('btnStartSim');
    const simBtnText = document.getElementById('simBtnText');
    const simDriveSelect = document.getElementById('simDriveSelect');
    const simProgressFill = document.getElementById('simProgressFill');
    const scanStateBadge = document.getElementById('scanStateBadge');
    const scanPctBadge = document.getElementById('scanPctBadge');
    const hudFiles = document.getElementById('hudFiles');
    const hudSpeed = document.getElementById('hudSpeed');
    const hudVolume = document.getElementById('hudVolume');
    const hudJunk = document.getElementById('hudJunk');
    const simTicker = document.getElementById('simTicker');
    const simConsole = document.getElementById('simConsole');

    const driveCapacityTitle = document.getElementById('driveCapacityTitle');
    const gaugeUsedText = document.getElementById('gaugeUsedText');
    const gaugeFreeText = document.getElementById('gaugeFreeText');
    const gaugeJunkText = document.getElementById('gaugeJunkText');

    const catVideoSize = document.getElementById('catVideoSize');
    const catImagesSize = document.getElementById('catImagesSize');
    const catPsdSize = document.getElementById('catPsdSize');
    const catJunkSize = document.getElementById('catJunkSize');
    const catCodeSize = document.getElementById('catCodeSize');

    const tiles = [
        document.getElementById('tileVideo'),
        document.getElementById('tileImages'),
        document.getElementById('tilePsd'),
        document.getElementById('tileJunk'),
        document.getElementById('tileCode')
    ];

    const samplePaths = [
        "C:\\Windows\\System32\\DriverStore\\FileRepository\\nv_dispi.inf_amd64",
        "C:\\Users\\admin\\AppData\\Local\\Temp\\scoped_dir_94812\\data.tmp",
        "C:\\Program Files\\Adobe\\Adobe Photoshop 2026\\Photoshop.exe",
        "C:\\Users\\admin\\.gradle\\caches\\modules-2\\files-2.1\\cache.bin",
        "C:\\Users\\admin\\source\\repos\\DiskScope\\Services\\ScannerService.cs",
        "C:\\Users\\admin\\.cargo\\registry\\cache\\index.crates.io-6f17d22bba15001f",
        "C:\\Users\\admin\\AppData\\Local\\Microsoft\\Edge\\User Data\\Default\\Cache",
        "C:\\Users\\admin\\Videos\\Captures\\Master_Render_4K_ProRes.mov",
        "C:\\Users\\admin\\Documents\\Photoshop\\hero_keyvisual_master.psb",
        "C:\\Windows\\assembly\\NativeImages_v4.0.30319_64\\mscorlib.dll"
    ];

    const driveProfiles = {
        "C:": {
            title: "C:\\ System NVMe SSD (512 GB Total Capacity)",
            targetFiles: 148250,
            targetVolume: 52.4,
            targetJunk: 14.8,
            maxSpeed: 58400,
            used: "382.0 GB",
            free: "130.0 GB",
            junk: "14.8 GB",
            cats: ["21.4 GB", "12.1 GB", "7.8 GB", "6.2 GB", "4.9 GB"]
        },
        "D:": {
            title: "D:\\ Developer Repositories SSD (1.0 TB Total Capacity)",
            targetFiles: 294100,
            targetVolume: 114.2,
            targetJunk: 28.6,
            maxSpeed: 61200,
            used: "680.0 GB",
            free: "320.0 GB",
            junk: "28.6 GB",
            cats: ["14.2 GB", "18.5 GB", "12.4 GB", "28.6 GB", "42.8 GB"]
        },
        "E:": {
            title: "E:\\ Photoshop & Media Archive HDD (4.0 TB Total Capacity)",
            targetFiles: 86400,
            targetVolume: 842.0,
            targetJunk: 4.2,
            maxSpeed: 49500,
            used: "3,200.0 GB",
            free: "800.0 GB",
            junk: "4.2 GB",
            cats: ["412.0 GB", "248.0 GB", "116.0 GB", "4.2 GB", "8.5 GB"]
        }
    };

    function updateDriveDisplay(driveKey) {
        const profile = driveProfiles[driveKey] || driveProfiles["C:"];
        if (driveCapacityTitle) driveCapacityTitle.textContent = profile.title;
        if (gaugeUsedText) gaugeUsedText.textContent = profile.used;
        if (gaugeFreeText) gaugeFreeText.textContent = profile.free;
        if (gaugeJunkText) gaugeJunkText.textContent = profile.junk;
        if (catVideoSize) catVideoSize.textContent = profile.cats[0];
        if (catImagesSize) catImagesSize.textContent = profile.cats[1];
        if (catPsdSize) catPsdSize.textContent = profile.cats[2];
        if (catJunkSize) catJunkSize.textContent = profile.cats[3];
        if (catCodeSize) catCodeSize.textContent = profile.cats[4];
    }

    if (simDriveSelect) {
        simDriveSelect.addEventListener('change', () => {
            updateDriveDisplay(simDriveSelect.value);
            if (simConsole) {
                simConsole.textContent = `> Target switch: ${simDriveSelect.value} selected.\n> Initializing disk geometry...\n> Ready for scan execution.`;
            }
        });
    }

    let isScanning = false;

    if (btnStartSim && hudFiles && hudSpeed && hudVolume && hudJunk && simTicker && simProgressFill) {
        btnStartSim.addEventListener('click', () => {
            if (isScanning) return;
            isScanning = true;
            btnStartSim.disabled = true;
            simBtnText.textContent = "Scanning...";
            scanStateBadge.textContent = "Scanning filesystem...";

            const driveVal = simDriveSelect ? simDriveSelect.value : "C:";
            const profile = driveProfiles[driveVal] || driveProfiles["C:"];

            // Reset tiles
            tiles.forEach(t => t && t.classList.remove('active'));

            let currentFiles = 0;
            const duration = 2400;
            const startTime = performance.now();
            let tickerIndex = 0;

            if (simConsole) {
                simConsole.textContent = `> Initializing traversal on ${driveVal}...\n> Threadpool allocated: 8 worker threads\n> Bounded channel: 20,000 slots\n> Ingestion streaming started.`;
            }

            const simInterval = setInterval(() => {
                const elapsed = performance.now() - startTime;
                const progress = Math.min(elapsed / duration, 1);
                const ease = 1 - Math.pow(1 - progress, 3);

                const pct = Math.floor(progress * 100);
                simProgressFill.style.width = `${pct}%`;
                scanPctBadge.textContent = `${pct}%`;

                currentFiles = Math.floor(ease * profile.targetFiles);
                hudFiles.textContent = currentFiles.toLocaleString();

                const currentSpeed = progress < 1 
                    ? Math.floor(ease * profile.maxSpeed * (0.85 + Math.random() * 0.3)) 
                    : profile.maxSpeed;
                hudSpeed.textContent = currentSpeed.toLocaleString() + ' f/s';

                hudVolume.textContent = (ease * profile.targetVolume).toFixed(1) + ' GB';
                hudJunk.textContent = (ease * profile.targetJunk).toFixed(1) + ' GB';

                simTicker.textContent = `[WAL Batch] Indexed: ${samplePaths[tickerIndex % samplePaths.length]}`;
                tickerIndex++;

                if (progress > 0.15 && tiles[0]) tiles[0].classList.add('active');
                if (progress > 0.35 && tiles[1]) tiles[1].classList.add('active');
                if (progress > 0.55 && tiles[2]) tiles[2].classList.add('active');
                if (progress > 0.75 && tiles[3]) tiles[3].classList.add('active');
                if (progress > 0.90 && tiles[4]) tiles[4].classList.add('active');

                if (progress >= 1) {
                    clearInterval(simInterval);
                    isScanning = false;
                    btnStartSim.disabled = false;
                    simBtnText.textContent = "Re-run Scan";
                    scanStateBadge.textContent = "Scan complete";
                    scanPctBadge.textContent = "100%";
                    hudFiles.textContent = profile.targetFiles.toLocaleString();
                    hudSpeed.textContent = "58,400 f/s (Peak)";
                    hudVolume.textContent = profile.targetVolume.toFixed(1) + ' GB';
                    hudJunk.textContent = profile.targetJunk.toFixed(1) + ' GB';
                    simTicker.textContent = `Scan complete: ${profile.targetFiles.toLocaleString()} files indexed in 2.4s. Cleanable junk: ${profile.targetJunk.toFixed(1)} GB.`;

                    if (simConsole) {
                        simConsole.textContent = `> Traversal completed on ${driveVal}\n> Indexed records: ${profile.targetFiles.toLocaleString()} files\n> Secondary B-tree indexes rebuilt in 0.18s\n> Passive WAL checkpoint executed\n> Reclaimable developer/system junk: ${profile.targetJunk.toFixed(1)} GB`;
                    }
                    showToast(`Scan complete: ${profile.targetFiles.toLocaleString()} files indexed on ${driveVal}.`);
                }
            }, 60);
        });
    }

    // =========================================================
    // 9. INTERACTIVE CHECKSUM COMPARATOR
    // =========================================================
    const verifyInput = document.getElementById('verifyInput');
    const verifyBtn = document.getElementById('verifyBtn');
    const verifyResult = document.getElementById('verifyResult');

    function executeVerification() {
        if (!verifyInput || !verifyResult) return;
        const inputVal = verifyInput.value.trim().toUpperCase();
        if (!inputVal) {
            verifyResult.style.display = 'none';
            return;
        }

        verifyResult.style.display = 'block';
        if (inputVal === OFFICIAL_HASH) {
            verifyResult.className = 'verify-status-banner match';
            verifyResult.textContent = '✓ Checksum Verified: Exact match with official release v1.0.0 (SHA-256 Validated).';
        } else {
            verifyResult.className = 'verify-status-banner mismatch';
            verifyResult.textContent = '✕ Hash Mismatch: Checksum does not match official release (Length: ' + inputVal.length + ' chars).';
        }
    }

    if (verifyBtn) {
        verifyBtn.addEventListener('click', executeVerification);
    }
    if (verifyInput) {
        verifyInput.addEventListener('input', executeVerification);
        verifyInput.addEventListener('keydown', (e) => {
            if (e.key === 'Enter') executeVerification();
        });
    }

    // =========================================================
    // 10. 1-CLICK CLIPBOARD UTILITIES
    // =========================================================
    const copyOfficialBtn = document.getElementById('copyOfficialHashBtn');
    if (copyOfficialBtn) {
        copyOfficialBtn.addEventListener('click', () => {
            navigator.clipboard.writeText(OFFICIAL_HASH).then(() => {
                showToast('Copied official SHA-256 digest to clipboard');
                const orig = copyOfficialBtn.textContent;
                copyOfficialBtn.textContent = 'Copied!';
                setTimeout(() => { copyOfficialBtn.textContent = orig; }, 2000);
            });
        });
    }

    const tableHashBtns = document.querySelectorAll('.copy-hash-btn');
    tableHashBtns.forEach(btn => {
        btn.addEventListener('click', () => {
            const hash = btn.getAttribute('data-hash') || OFFICIAL_HASH;
            navigator.clipboard.writeText(hash).then(() => {
                showToast('SHA-256 hash copied to clipboard');
                const orig = btn.textContent;
                btn.textContent = 'Copied!';
                setTimeout(() => { btn.textContent = orig; }, 2000);
            });
        });
    });

    const copyCmdBtns = document.querySelectorAll('.copy-cmd-btn');
    copyCmdBtns.forEach(btn => {
        btn.addEventListener('click', () => {
            const targetId = btn.getAttribute('data-target');
            const targetEl = document.getElementById(targetId);
            if (targetEl) {
                const cmdText = targetEl.textContent.trim();
                navigator.clipboard.writeText(cmdText).then(() => {
                    showToast('Command copied to clipboard');
                    const orig = btn.textContent;
                    btn.textContent = 'Copied!';
                    setTimeout(() => { btn.textContent = orig; }, 2000);
                });
            }
        });
    });

    // Initialize Setup Wizard
    updateWizardUI();
});

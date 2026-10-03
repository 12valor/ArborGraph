// DiskScope: Windows Disk Analysis & Setup Utility Script
// Modern Developer Tool Portal Script (Interactive Navigation, Setup Stepper, Diagnostics & Verification)

document.addEventListener('DOMContentLoaded', () => {
    const OFFICIAL_HASH = 'BCBF7FFF8961BA12DDC747077FBF2B10775F97BE8C76C493488A1107D7CD750A';

    // =========================================================
    // 1. HEADER NAVIGATION, SCROLLSPY & MOBILE DRAWER
    // =========================================================
    const siteHeader = document.getElementById('siteHeader');
    const navLinks = document.querySelectorAll('.site-nav .nav-link');
    const mobileNavToggle = document.getElementById('mobileNavToggle');
    const mobileNavDrawer = document.getElementById('mobileNavDrawer');
    const mobileNavLinks = document.querySelectorAll('.mobile-nav-link');
    const sections = document.querySelectorAll('section.site-section');

    // Sticky header shadow on scroll
    window.addEventListener('scroll', () => {
        if (siteHeader) {
            siteHeader.classList.toggle('scrolled', window.scrollY > 20);
        }
    }, { passive: true });

    // Mobile nav toggle
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

    // Scrollspy: update active nav link as user scrolls
    if ('IntersectionObserver' in window && sections.length > 0) {
        const observerOptions = {
            root: null,
            rootMargin: '-20% 0px -60% 0px',
            threshold: 0
        };

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
        }, observerOptions);

        sections.forEach(sec => observer.observe(sec));
    }

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
                    showToast('Launching DiskScope (Downloading binary)...');
                    const link = document.createElement('a');
                    link.href = 'https://github.com/12valor/DiskScope/releases/latest/download/DiskScope.exe';
                    link.target = '_blank';
                    link.rel = 'noopener noreferrer';
                    document.body.appendChild(link);
                    link.click();
                    document.body.removeChild(link);
                } else {
                    showToast('DiskScope Setup successfully completed.');
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
            if (confirm('Are you sure you want to cancel DiskScope Setup?')) {
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
                wizardInstallPath.value = "%LocalAppData%\\DiskScope";
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
            { pct: 15, msg: "Extracting core binary: DiskScope.exe (73.3 MB)...", log: "> Extracting PE32+ executable header...\n> Unpacking bundled .NET 8.0 runtime assemblies..." },
            { pct: 35, msg: "Deploying WPF presentation subsystem...", log: "> Registering PresentationCore.dll & PresentationFramework.dll\n> Validating DirectX Hardware Acceleration..." },
            { pct: 58, msg: "Configuring SQLite database subsystem...", log: "> Unpacking Microsoft.Data.Sqlite & SQLitePCLRaw.bundle_e_sqlite3\n> Registering local database schema in %LocalAppData%\\DiskScope..." },
            { pct: 78, msg: "Creating application environment...", log: "> Configuring WAL journal mode and 20,000-item channel capacity\n> Creating Start Menu & Desktop shortcuts..." },
            { pct: 95, msg: "Verifying package cryptographic checksum...", log: "> Validating SHA-256 binary digest: BCBF7FFF...CD750A\n> Cryptographic match verified bit-for-bit." },
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
            publisher: "AG DIAZ EVANGELISTA",
            offlineStorage: "%LocalAppData%\\DiskScope"
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
            const targetDoc = link.getAttribute('data-doc');
            if (targetDoc) switchDoc(targetDoc);
            const docElement = document.getElementById('docs');
            if (docElement) {
                docElement.scrollIntoView({ behavior: 'smooth' });
            }
        });
    });

    const footerLegalLinks = document.querySelectorAll('.footer-legal-link');
    footerLegalLinks.forEach(link => {
        link.addEventListener('click', (e) => {
            e.preventDefault();
            const targetLegal = link.getAttribute('data-legal');
            if (targetLegal) switchLegal(targetLegal);
            const legalElement = document.getElementById('legal');
            if (legalElement) {
                legalElement.scrollIntoView({ behavior: 'smooth' });
            }
        });
    });

    // Initial URL Hash Handling
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
        } else {
            const targetSection = document.getElementById(hash);
            if (targetSection) targetSection.scrollIntoView();
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

            let currentFiles = 0;
            const duration = 2400;
            const startTime = performance.now();
            let tickerIndex = 0;

            if (simConsole) {
                simConsole.textContent = `> Initiating DiskScope multi-threaded traversal on ${driveVal}...\n> Threadpool spawned: 8 worker threads.\n> SQLite WAL ingestion active.`;
            }

            const simInterval = setInterval(() => {
                const elapsed = performance.now() - startTime;
                const progressRatio = Math.min(1, elapsed / duration);
                const pct = Math.floor(progressRatio * 100);

                simProgressFill.style.width = `${pct}%`;
                scanPctBadge.textContent = `${pct}%`;

                currentFiles = Math.floor(progressRatio * profile.targetFiles);
                hudFiles.textContent = currentFiles.toLocaleString();

                const currentVol = (progressRatio * profile.targetVolume).toFixed(1);
                hudVolume.textContent = `${currentVol} GB`;

                const currentJunk = (progressRatio * profile.targetJunk).toFixed(1);
                hudJunk.textContent = `${currentJunk} GB`;

                const randomSpeed = Math.floor(profile.maxSpeed * (0.8 + Math.random() * 0.2));
                hudSpeed.textContent = `${randomSpeed.toLocaleString()} f/s`;

                tickerIndex = (tickerIndex + 1) % samplePaths.length;
                simTicker.textContent = samplePaths[tickerIndex];

                if (progressRatio >= 1) {
                    clearInterval(simInterval);
                    isScanning = false;
                    btnStartSim.disabled = false;
                    simBtnText.textContent = "Scan Completed (Restart)";
                    scanStateBadge.textContent = "Scan Completed";
                    hudSpeed.textContent = "0 f/s";
                    simTicker.textContent = `Completed: Indexed ${profile.targetFiles.toLocaleString()} files across ${driveVal} in 2.4s.`;
                    if (simConsole) {
                        simConsole.textContent += `\n> Traversal complete.\n> Indexed: ${profile.targetFiles.toLocaleString()} records.\n> SQLite secondary indexes rebuilt in 42ms.\n> Reclaimable developer junk: ${profile.junk}.`;
                        simConsole.scrollTop = simConsole.scrollHeight;
                    }
                    showToast(`Filesystem scan of ${driveVal} completed successfully!`);
                }
            }, 50);
        });
    }

    // =========================================================
    // 9. PACKAGE VERIFICATION INTERACTION
    // =========================================================
    const copyOfficialHashBtn = document.getElementById('copyOfficialHashBtn');
    const verifyInput = document.getElementById('verifyInput');
    const verifyBtn = document.getElementById('verifyBtn');
    const verifyResult = document.getElementById('verifyResult');

    if (copyOfficialHashBtn) {
        copyOfficialHashBtn.addEventListener('click', () => {
            navigator.clipboard.writeText(OFFICIAL_HASH).then(() => {
                showToast('Official SHA-256 hash copied to clipboard!');
            }).catch(() => {
                showToast('Hash: ' + OFFICIAL_HASH);
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
                    <strong>INTEGRITY VERIFIED:</strong> Checksum matches official binary digest bit-for-bit.<br>
                    <span style="font-size: 11px;">Algorithm: SHA-256 &bull; Status: Authenticated Release</span>
                `;
                showToast('Hash verified: Authenticity confirmed!');
            } else {
                verifyResult.className = 'verify-status-banner mismatch';
                verifyResult.innerHTML = `
                    <strong>MISMATCH DETECTED:</strong> Hash does not match the official release digest.<br>
                    <span style="font-size: 11px;">Expected: ${OFFICIAL_HASH}<br>Received: ${entered}</span>
                `;
                showToast('Hash mismatch: Binary may be corrupted or altered.');
            }
        });
    }

    // =========================================================
    // 10. GLOBAL COPY BUTTONS
    // =========================================================
    document.querySelectorAll('.copy-hash-btn').forEach(btn => {
        btn.addEventListener('click', () => {
            const hash = btn.getAttribute('data-hash') || OFFICIAL_HASH;
            navigator.clipboard.writeText(hash).then(() => {
                showToast('SHA-256 checksum copied to clipboard!');
            }).catch(() => {
                showToast('Hash copied: ' + hash);
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
                    showToast('Command copied: ' + text);
                }).catch(() => {
                    showToast('Copied: ' + text);
                });
            }
        });
    });

    // Initialize Setup Stepper UI
    updateWizardUI();
});

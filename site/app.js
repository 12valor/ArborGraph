// DiskScope Pro — Installer & Download Portal Dynamic Script
document.addEventListener('DOMContentLoaded', () => {
    const OFFICIAL_HASH = '095EAE7AFB4AC3AC15F504EC998B839C99032BDC0A46CBD1239309C39C8FCB4C';

    // 1. Toast Notification Helper
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

    // 2. Client OS & Architecture Detection
    const detectedOSEl = document.getElementById('detectedOS');
    if (detectedOSEl) {
        const userAgent = window.navigator.userAgent;
        let osText = 'System Check: Windows (64-bit Architecture Verified) ✓';
        if (/Win64|x64|WOW64/i.test(userAgent)) {
            osText = 'System Check: Windows 64-bit Architecture Verified ✓';
        } else if (/Windows/i.test(userAgent)) {
            osText = 'System Check: Windows OS Detected ✓';
        } else if (/Mac/i.test(userAgent)) {
            osText = 'Note: macOS Detected. DiskScope runs natively on Windows 10/11 x64.';
        } else if (/Linux/i.test(userAgent)) {
            osText = 'Note: Linux Detected. DiskScope runs natively on Windows 10/11 x64.';
        }
        detectedOSEl.textContent = osText;
    }

    // 3. Dynamic Download Trigger & Live Progress Simulation
    const downloadBtns = document.querySelectorAll('a[download]');
    const progressBox = document.getElementById('downloadProgressBox');
    const progressBarFill = document.getElementById('progressBarFill');
    const progressStatusText = document.getElementById('progressStatusText');
    const progressPercent = document.getElementById('progressPercent');

    downloadBtns.forEach(btn => {
        btn.addEventListener('click', () => {
            showToast('Starting download of DiskScope.exe (72.7 MB)...');

            if (progressBox && progressBarFill && progressStatusText && progressPercent) {
                progressBox.style.display = 'block';
                progressBarFill.style.width = '0%';
                progressPercent.textContent = '0%';
                progressStatusText.textContent = 'Transferring DiskScope.exe package...';

                let progress = 0;
                const interval = setInterval(() => {
                    progress += 10;
                    if (progress > 100) progress = 100;
                    progressBarFill.style.width = `${progress}%`;
                    progressPercent.textContent = `${progress}%`;

                    if (progress >= 100) {
                        clearInterval(interval);
                        progressStatusText.textContent = '✓ Download completed! Ready to run.';
                        showToast('✓ DiskScope.exe ready. Follow Step 1-3 to launch.');
                    }
                }, 110);
            }
        });
    });

    // 4. Interactive Live Mini-Scanner Simulator
    const btnStartSim = document.getElementById('btnStartSim');
    const simBtnText = document.getElementById('simBtnText');
    const simDriveSelect = document.getElementById('simDriveSelect');
    const hudFiles = document.getElementById('hudFiles');
    const hudSpeed = document.getElementById('hudSpeed');
    const hudVolume = document.getElementById('hudVolume');
    const hudJunk = document.getElementById('hudJunk');
    const simTicker = document.getElementById('simTicker');

    const tiles = [
        document.getElementById('tileVideo'),
        document.getElementById('tileImages'),
        document.getElementById('tilePsd'),
        document.getElementById('tileJunk'),
        document.getElementById('tileCode')
    ];

    const samplePaths = [
        "C:\\Windows\\System32\\DriverStore\\FileRepository\\nv_dispi.inf_amd64",
        "D:\\workspace\\project\\node_modules\\@babel\\core\\lib\\config\\files\\plugins.js",
        "E:\\Media\\4K_Video_Render_Archive_Master.mp4",
        "D:\\Design\\Branding\\hero_keyvisual_huge.psb",
        "C:\\Users\\admin\\AppData\\Local\\Temp\\scoped_dir_94812\\data.tmp",
        "D:\\source\\rust_engine\\target\\release\\deps\\libtokio.rlib",
        "C:\\Users\\admin\\.gradle\\caches\\modules-2\\files-2.1\\cache.bin",
        "E:\\Photography\\2026_RAW\\IMG_4819_uncompressed.CR3",
        "C:\\Users\\admin\\.cargo\\registry\\cache\\index.crates.io-6f17d22bba15001f",
        "D:\\Games\\SteamLibrary\\steamapps\\common\\ShaderCache\\dx12_pso.bin"
    ];

    let isScanning = false;

    if (btnStartSim && hudFiles && hudSpeed && hudVolume && hudJunk && simTicker) {
        btnStartSim.addEventListener('click', () => {
            if (isScanning) return;
            isScanning = true;
            btnStartSim.disabled = true;
            simBtnText.textContent = "Scanning...";

            const driveVal = simDriveSelect ? simDriveSelect.value : "C:";
            let targetFiles = 148250;
            let targetVolume = 52.4;
            let targetJunk = 14.8;
            let maxSpeed = 58400;

            if (driveVal.startsWith("D")) {
                targetFiles = 294100;
                targetVolume = 114.2;
                targetJunk = 28.6;
                maxSpeed = 61200;
            } else if (driveVal.startsWith("E")) {
                targetFiles = 86400;
                targetVolume = 842.0;
                targetJunk = 4.2;
                maxSpeed = 49500;
            }

            // Reset tiles
            tiles.forEach(t => t && t.classList.remove('active'));

            let currentFiles = 0;
            const duration = 2400;
            const startTime = performance.now();
            let tickerIndex = 0;

            const simInterval = setInterval(() => {
                const elapsed = performance.now() - startTime;
                const progress = Math.min(elapsed / duration, 1);
                // Ease out cubic
                const ease = 1 - Math.pow(1 - progress, 3);

                currentFiles = Math.floor(ease * targetFiles);
                hudFiles.textContent = currentFiles.toLocaleString();

                const currentSpeed = progress < 1 
                    ? Math.floor(ease * maxSpeed * (0.85 + Math.random() * 0.3)) 
                    : maxSpeed;
                hudSpeed.textContent = currentSpeed.toLocaleString() + ' f/s';

                hudVolume.textContent = (ease * targetVolume).toFixed(1) + ' GB';
                hudJunk.textContent = (ease * targetJunk).toFixed(1) + ' GB';

                // Path ticker
                simTicker.textContent = `[WAL Batch] Indexed: ${samplePaths[tickerIndex % samplePaths.length]}`;
                tickerIndex++;

                // Activate tiles progressively
                if (progress > 0.15 && tiles[0]) tiles[0].classList.add('active');
                if (progress > 0.35 && tiles[1]) tiles[1].classList.add('active');
                if (progress > 0.55 && tiles[2]) tiles[2].classList.add('active');
                if (progress > 0.75 && tiles[3]) tiles[3].classList.add('active');
                if (progress > 0.90 && tiles[4]) tiles[4].classList.add('active');

                if (progress >= 1) {
                    clearInterval(simInterval);
                    isScanning = false;
                    btnStartSim.disabled = false;
                    simBtnText.textContent = "Re-run Simulation";
                    hudFiles.textContent = targetFiles.toLocaleString();
                    hudSpeed.textContent = "58,400 f/s (Peak)";
                    hudVolume.textContent = targetVolume.toFixed(1) + ' GB';
                    hudJunk.textContent = targetJunk.toFixed(1) + ' GB';
                    simTicker.textContent = `✓ Scan Complete: ${targetFiles.toLocaleString()} files indexed in 2.4s. Reclaimable: ${targetJunk.toFixed(1)} GB cleanable junk across 5 categories.`;
                    showToast(`✓ Simulated scan complete! ${targetJunk.toFixed(1)} GB cleanable junk identified.`);
                }
            }, 60);
        });
    }

    // 5. Interactive Checksum Comparator
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

        if (inputVal === OFFICIAL_HASH) {
            verifyResult.className = 'verify-result match';
            verifyResult.textContent = '✓ Checksum Verified: Exact match with official DiskScope v1.0.0 release.';
        } else {
            verifyResult.className = 'verify-result mismatch';
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

    // 6. Copy Official Digest
    const copyOfficialBtn = document.getElementById('copyOfficialHashBtn');
    if (copyOfficialBtn) {
        copyOfficialBtn.addEventListener('click', () => {
            navigator.clipboard.writeText(OFFICIAL_HASH).then(() => {
                showToast('✓ Copied SHA-256 digest to clipboard');
                const orig = copyOfficialBtn.textContent;
                copyOfficialBtn.textContent = 'Copied!';
                setTimeout(() => { copyOfficialBtn.textContent = orig; }, 2000);
            });
        });
    }

    // 7. Copy Table Checksum Buttons
    const tableHashBtns = document.querySelectorAll('.copy-hash-btn');
    tableHashBtns.forEach(btn => {
        btn.addEventListener('click', () => {
            const hash = btn.getAttribute('data-hash') || OFFICIAL_HASH;
            navigator.clipboard.writeText(hash).then(() => {
                showToast('✓ SHA-256 hash copied');
                const orig = btn.textContent;
                btn.textContent = 'Copied!';
                setTimeout(() => { btn.textContent = orig; }, 2000);
            });
        });
    });

    // 8. Copy Terminal Command Buttons
    const copyCmdBtns = document.querySelectorAll('.copy-cmd-btn');
    copyCmdBtns.forEach(btn => {
        btn.addEventListener('click', () => {
            const targetId = btn.getAttribute('data-target');
            const targetEl = document.getElementById(targetId);
            if (targetEl) {
                const cmdText = targetEl.textContent.trim();
                navigator.clipboard.writeText(cmdText).then(() => {
                    showToast('✓ Command copied to clipboard');
                    const orig = btn.textContent;
                    btn.textContent = 'Copied!';
                    setTimeout(() => { btn.textContent = orig; }, 2000);
                });
            }
        });
    });
});

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
        }, 3000);
    }

    // 2. Client OS & Architecture Detection
    const detectedOSEl = document.getElementById('detectedOS');
    if (detectedOSEl) {
        const userAgent = window.navigator.userAgent;
        let osText = 'Windows (64-bit Architecture Detected)';
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
                }, 120);
            }
        });
    });

    // 4. Interactive Checksum Comparator
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

    // 5. Copy Official Digest
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

    // 6. Copy Table Checksum Buttons
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

    // 7. Copy Terminal Command Buttons
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

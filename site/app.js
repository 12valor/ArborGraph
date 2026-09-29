// DiskScope Pro Landing Website Interactions
document.addEventListener('DOMContentLoaded', () => {
    // 1. Copy Hash Button
    const copyBtn = document.getElementById('copyHashBtn');
    const hashVal = document.getElementById('hashVal');
    const toast = document.getElementById('toast');

    function showToast(message) {
        if (!toast) return;
        toast.textContent = message;
        toast.classList.add('show');
        setTimeout(() => {
            toast.classList.remove('show');
        }, 3000);
    }

    if (copyBtn && hashVal) {
        copyBtn.addEventListener('click', () => {
            const textToCopy = hashVal.textContent.trim();
            navigator.clipboard.writeText(textToCopy).then(() => {
                showToast('✓ SHA-256 Checksum copied to clipboard!');
                copyBtn.textContent = 'Copied!';
                setTimeout(() => {
                    copyBtn.textContent = 'Copy Hash';
                }, 2000);
            }).catch(() => {
                showToast('Failed to copy. Please select manually.');
            });
        });
    }

    // 2. Interactive Treemap Inspector
    const simBlocks = document.querySelectorAll('.sim-block');
    const inspectorPill = document.getElementById('treemapInspector');

    simBlocks.forEach(block => {
        block.addEventListener('mouseenter', () => {
            const name = block.getAttribute('data-name');
            const size = block.getAttribute('data-size');
            const cat = block.getAttribute('data-category');
            if (inspectorPill) {
                inspectorPill.innerHTML = `Hovered: <strong style="color: #fff;">${name}</strong> (${size}) • <span style="opacity: 0.8">${cat}</span>`;
            }
        });

        block.addEventListener('click', () => {
            const name = block.getAttribute('data-name');
            const size = block.getAttribute('data-size');
            showToast(`Selected: ${name} (${size})`);
        });
    });

    // 3. Download Trigger Toast
    const downloadBtns = document.querySelectorAll('.btn-download');
    downloadBtns.forEach(btn => {
        btn.addEventListener('click', () => {
            showToast('🚀 Downloading DiskScope.exe (72.7 MB)...');
        });
    });

    // 4. Live Stats Counter Animation on Hero
    const statCounter = document.getElementById('statCounter');
    if (statCounter) {
        let count = 0;
        const target = 142850;
        const duration = 1500;
        const start = performance.now();

        function animateCount(now) {
            const progress = Math.min((now - start) / duration, 1);
            // Ease out quad
            const easeProgress = 1 - (1 - progress) * (1 - progress);
            const current = Math.floor(easeProgress * target);
            statCounter.textContent = current.toLocaleString() + ' files';

            if (progress < 1) {
                requestAnimationFrame(animateCount);
            }
        }
        requestAnimationFrame(animateCount);
    }
});

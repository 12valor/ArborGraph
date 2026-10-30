/**
 * ArborGraph — Desktop Product Site Script
 * Lightweight, zero dependencies, accessible.
 */
(() => {
  'use strict';

  // --- Mobile Navigation Toggle ---
  const menuToggle = document.querySelector('.menu-toggle');
  const primaryNav = document.querySelector('#primary-nav');

  if (menuToggle && primaryNav) {
    const closeMenu = () => {
      primaryNav.classList.remove('is-open');
      menuToggle.setAttribute('aria-expanded', 'false');
      menuToggle.setAttribute('aria-label', 'Open navigation');
    };

    menuToggle.addEventListener('click', () => {
      const isOpen = primaryNav.classList.contains('is-open');
      primaryNav.classList.toggle('is-open', !isOpen);
      menuToggle.setAttribute('aria-expanded', String(!isOpen));
      menuToggle.setAttribute('aria-label', !isOpen ? 'Close navigation' : 'Open navigation');
    });

    primaryNav.addEventListener('click', (event) => {
      if (event.target.closest('a')) {
        closeMenu();
      }
    });

    document.addEventListener('keydown', (event) => {
      if (event.key === 'Escape' && primaryNav.classList.contains('is-open')) {
        closeMenu();
      }
    });

    document.addEventListener('click', (event) => {
      if (!primaryNav.contains(event.target) && !menuToggle.contains(event.target)) {
        closeMenu();
      }
    });

    window.matchMedia('(min-width: 769px)').addEventListener('change', (e) => {
      if (e.matches) closeMenu();
    });
  }

  // --- Tagline Reveal Section (B11) ---
  const taglineEl = document.querySelector('[data-reveal-words="true"]');
  if (taglineEl) {
    const rawText = taglineEl.textContent.trim();
    const words = rawText.split(/\s+/);
    taglineEl.innerHTML = words.map((w) => `<span class="reveal-word">${w}</span>`).join(' ');

    const wordSpans = taglineEl.querySelectorAll('.reveal-word');
    if ('IntersectionObserver' in window && !window.matchMedia('(prefers-reduced-motion: reduce)').matches) {
      const observer = new IntersectionObserver(
        (entries) => {
          entries.forEach((entry) => {
            if (entry.isIntersecting) {
              wordSpans.forEach((span, idx) => {
                setTimeout(() => {
                  span.classList.add('is-revealed');
                }, idx * 45);
              });
              observer.unobserve(entry.target);
            }
          });
        },
        { threshold: 0.25 }
      );
      observer.observe(taglineEl);
    } else {
      wordSpans.forEach((span) => span.classList.add('is-revealed'));
    }
  }

  // --- Interactive Workflow Tabs ---
  const tabButtons = document.querySelectorAll('.workflow-tab');
  const panels = document.querySelectorAll('.workflow-panel');

  if (tabButtons.length > 0 && panels.length > 0) {
    tabButtons.forEach((tab, index) => {
      tab.addEventListener('click', () => {
        const targetPanelId = tab.getAttribute('aria-controls');

        tabButtons.forEach((t) => {
          t.classList.remove('is-active');
          t.setAttribute('aria-selected', 'false');
        });

        panels.forEach((p) => {
          p.classList.remove('is-active');
          p.hidden = true;
        });

        tab.classList.add('is-active');
        tab.setAttribute('aria-selected', 'true');

        const activePanel = document.getElementById(targetPanelId);
        if (activePanel) {
          activePanel.classList.add('is-active');
          activePanel.hidden = false;
        }
      });

      // Keyboard arrow navigation between tabs
      tab.addEventListener('keydown', (e) => {
        let newIndex = null;
        if (e.key === 'ArrowRight') {
          newIndex = (index + 1) % tabButtons.length;
        } else if (e.key === 'ArrowLeft') {
          newIndex = (index - 1 + tabButtons.length) % tabButtons.length;
        }
        if (newIndex !== null) {
          tabButtons[newIndex].focus();
          tabButtons[newIndex].click();
        }
      });
    });
  }

  // --- 1-Click Clipboard Copy ---
  async function copyText(value) {
    if (navigator.clipboard && navigator.clipboard.writeText) {
      await navigator.clipboard.writeText(value);
      return;
    }
    const helper = document.createElement('textarea');
    helper.value = value;
    helper.setAttribute('readonly', '');
    helper.style.position = 'fixed';
    helper.style.opacity = '0';
    document.body.appendChild(helper);
    helper.select();
    const success = document.execCommand('copy');
    helper.remove();
    if (!success) throw new Error('Clipboard write failed');
  }

  document.querySelectorAll('[data-copy]').forEach((btn) => {
    btn.addEventListener('click', async () => {
      const targetId = btn.getAttribute('data-copy');
      const targetEl = document.getElementById(targetId);
      if (!targetEl) return;

      const originalText = btn.textContent;
      try {
        await copyText(targetEl.textContent.trim());
        btn.textContent = 'Copied!';
      } catch {
        btn.textContent = 'Failed';
      }
      setTimeout(() => {
        btn.textContent = originalText;
      }, 2200);
    });
  });

  // --- Interactive Hash Verifier ---
  const officialHashEl = document.getElementById('official-hash');
  const enteredInput = document.getElementById('entered-hash');
  const compareBtn = document.getElementById('compare-hash');
  const verifyResult = document.getElementById('verify-result');

  if (officialHashEl && enteredInput && compareBtn && verifyResult) {
    const officialDigest = officialHashEl.textContent.trim().toUpperCase();

    function performComparison() {
      const entered = enteredInput.value.trim().toUpperCase();
      verifyResult.className = '';

      if (!entered) {
        verifyResult.textContent = 'Please paste a SHA-256 hash first.';
        verifyResult.classList.add('error');
        return;
      }

      if (!/^[A-F0-9]{64}$/.test(entered)) {
        verifyResult.textContent = 'Invalid format. A SHA-256 hash must be exactly 64 hexadecimal characters.';
        verifyResult.classList.add('error');
        return;
      }

      if (entered === officialDigest) {
        verifyResult.textContent = '✓ Checksum Match. This installer matches the verified official v1.3.3 release.';
        verifyResult.classList.add('success');
      } else {
        verifyResult.textContent = '✗ Checksum Mismatch! Do not run this file. Check the download source.';
        verifyResult.classList.add('error');
      }
    }

    compareBtn.addEventListener('click', performComparison);
    enteredInput.addEventListener('keydown', (e) => {
      if (e.key === 'Enter') {
        performComparison();
      }
    });
  }
})();

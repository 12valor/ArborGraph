(() => {
  const menu = document.querySelector('.menu-toggle');
  const nav = document.querySelector('#primary-nav');
  const closeMenu = () => {
    nav.classList.remove('open');
    menu.setAttribute('aria-expanded', 'false');
    menu.setAttribute('aria-label', 'Open navigation');
  };
  menu.addEventListener('click', () => {
    const open = menu.getAttribute('aria-expanded') !== 'true';
    nav.classList.toggle('open', open);
    menu.setAttribute('aria-expanded', String(open));
    menu.setAttribute('aria-label', open ? 'Close navigation' : 'Open navigation');
  });
  nav.addEventListener('click', (event) => { if (event.target.closest('a')) closeMenu(); });
  document.addEventListener('keydown', (event) => { if (event.key === 'Escape') closeMenu(); });
  document.addEventListener('click', (event) => { if (!nav.contains(event.target) && !menu.contains(event.target)) closeMenu(); });
  window.matchMedia('(min-width: 781px)').addEventListener('change', closeMenu);

  async function copyText(value) {
    if (navigator.clipboard?.writeText) {
      await navigator.clipboard.writeText(value);
      return;
    }
    const helper = document.createElement('textarea');
    helper.value = value;
    helper.setAttribute('readonly', '');
    helper.style.position = 'fixed';
    helper.style.opacity = '0';
    document.body.append(helper);
    helper.select();
    const copied = document.execCommand('copy');
    helper.remove();
    if (!copied) throw new Error('Clipboard access is unavailable.');
  }

  document.querySelectorAll('[data-copy]').forEach((button) => {
    button.addEventListener('click', async () => {
      const target = document.getElementById(button.dataset.copy);
      if (!target) return;
      const original = button.textContent;
      try {
        await copyText(target.textContent.trim());
        button.textContent = 'Copied';
      } catch {
        button.textContent = 'Copy failed';
      }
      window.setTimeout(() => { button.textContent = original; }, 2400);
    });
  });

  const hash = document.getElementById('official-hash').textContent.trim().toUpperCase();
  const input = document.getElementById('entered-hash');
  const result = document.getElementById('verify-result');
  function compareHash() {
    const entered = input.value.trim().toUpperCase();
    result.className = '';
    if (!/^[A-F0-9]{64}$/.test(entered)) {
      result.textContent = 'Enter the full 64-character SHA-256 digest.';
      result.classList.add('error');
      return;
    }
    const match = entered === hash;
    result.textContent = match ? 'Match. This digest equals the published installer checksum.' : 'No match. Do not run this installer; check the file and download source.';
    result.classList.add(match ? 'success' : 'error');
  }
  document.getElementById('compare-hash').addEventListener('click', compareHash);
  input.addEventListener('keydown', (event) => { if (event.key === 'Enter') compareHash(); });

  if ('IntersectionObserver' in window && !window.matchMedia('(prefers-reduced-motion: reduce)').matches) {
    const reveal = new IntersectionObserver((entries) => {
      for (const entry of entries) {
        if (entry.isIntersecting) {
          entry.target.classList.add('is-visible');
          reveal.unobserve(entry.target);
        }
      }
    }, { threshold: 0.08 });
    document.querySelectorAll('.showcase h2, .showcase-image, .section-intro h2, .feature-row, .process h2, .download-layout, .verification h2, .verify-tool, .docs h2, .doc-list article, .release h2, .legal h2').forEach((item) => {
      item.classList.add('will-reveal');
      reveal.observe(item);
    });
  }
})();

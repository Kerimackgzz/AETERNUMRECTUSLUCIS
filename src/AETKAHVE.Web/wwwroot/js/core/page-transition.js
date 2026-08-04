// AETERNUM RECTUS LUCIS — Page Transition Overlay
// Ayni-origin GET navigasyonlarinda kisa bir gecis overlay'i gosterir.
// Form submit, yeni sekme/pencere, modifier-key'li tiklama, download, hash-only
// ve farkli origin linklerine hic dokunmaz (native davranis korunur).

// CSS gecis suresi (page-transition.css) ile senkron: navigasyon, overlay tam
// opaklasmadan biraz once tetiklenir ki sayfa degisimi kesintisiz hissettirsin.
const NAVIGATE_DELAY_MS = 220;

function getOverlay() {
  return document.querySelector("[data-page-transition-overlay]");
}

function shouldIntercept(anchor, event) {
  if (!anchor || !anchor.getAttribute("href")) return false;
  if (anchor.target && anchor.target !== "_self") return false;
  if (anchor.hasAttribute("download")) return false;
  if (anchor.dataset.noTransition !== undefined) return false;
  if (event.defaultPrevented || event.button !== 0) return false;
  if (event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) return false;

  let url;
  try {
    url = new URL(anchor.href, window.location.href);
  } catch (err) {
    return false;
  }
  if (url.origin !== window.location.origin) return false;
  if (url.pathname === window.location.pathname && url.search === window.location.search && url.hash) {
    return false; // sayfa-ici anchor, engelleme
  }
  return true;
}

export function initPageTransitionOverlay() {
  const overlay = getOverlay();
  if (!overlay) return null;

  const reducedMotion = window.matchMedia("(prefers-reduced-motion: reduce)").matches;

  const clearOverlay = () => {
    overlay.classList.remove("is-active");
    overlay.hidden = true;
  };

  // Ilk yukleme ve bfcache geri donusunde (back/forward) overlay her zaman temiz baslar;
  // popstate'e kasitli olarak dokunulmuyor, tarayicinin geri/ileri akisi bozulmaz.
  window.addEventListener("pageshow", clearOverlay);
  clearOverlay();

  const onClick = (event) => {
    const anchor = event.target instanceof Element ? event.target.closest("a[href]") : null;
    if (!shouldIntercept(anchor, event)) return;

    event.preventDefault();
    const destination = anchor.href;

    if (reducedMotion) {
      window.location.assign(destination);
      return;
    }

    overlay.hidden = false;
    void overlay.offsetWidth; // reflow: transition'i garanti tetikle
    overlay.classList.add("is-active");

    window.setTimeout(() => {
      window.location.assign(destination);
    }, NAVIGATE_DELAY_MS);
  };

  document.addEventListener("click", onClick);

  return {
    destroy() {
      document.removeEventListener("click", onClick);
      window.removeEventListener("pageshow", clearOverlay);
    },
  };
}

if (typeof document !== "undefined") {
  if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", () => initPageTransitionOverlay());
  } else {
    initPageTransitionOverlay();
  }
}

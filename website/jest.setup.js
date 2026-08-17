import '@testing-library/jest-dom';

// jsdom não implementa window.matchMedia — usado pelo FilterBar (Issue #171) para decidir
// entre o layout desktop (linha única) e mobile/tablet (summary + drawer). Default `matches:
// false` (mobile) — os testes do FilterBar abrem o drawer explicitamente quando precisam
// interagir com os controles, refletindo o comportamento real em viewport estreita.
if (typeof window !== 'undefined' && !window.matchMedia) {
  window.matchMedia = function matchMedia(query) {
    return {
      matches: false,
      media: query,
      onchange: null,
      addListener: () => {},
      removeListener: () => {},
      addEventListener: () => {},
      removeEventListener: () => {},
      dispatchEvent: () => false,
    };
  };
}

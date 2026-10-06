(() => {
  "use strict";
  const demo = document.getElementById("display-demo");
  const toggle = document.getElementById("animation-toggle");
  if (!demo || !toggle) return;

  const reducedMotion = window.matchMedia("(prefers-reduced-motion: reduce)");
  const setPlaying = (playing) => {
    demo.dataset.playing = String(playing);
    toggle.textContent = playing ? "Pause animation" : "Play animation";
  };

  const syncPreference = () => {
    if (reducedMotion.matches) {
      demo.removeAttribute("data-animated");
      setPlaying(false);
      toggle.hidden = true;
    } else {
      demo.dataset.animated = "";
      setPlaying(true);
      toggle.hidden = false;
    }
  };

  toggle.addEventListener("click", () => {
    setPlaying(demo.dataset.playing !== "true");
  });
  reducedMotion.addEventListener("change", syncPreference);
  syncPreference();
})();

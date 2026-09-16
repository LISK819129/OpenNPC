/* OpenNPC landing page.
 *  1. A small population walks behind the wordmark (Blender-rendered sprite sheet).
 *  2. "Same question, different minds" cards.
 *  3. Unity WebGL loader: poster → loading % → running, with fullscreen and a clear error state.
 */
(() => {
  "use strict";

  // ------------------------------------------------------------------ crowd
  const SPRITE = { src: "assets/stickman-walk.png", frames: 12, w: 96, h: 128 };
  const reducedMotion = window.matchMedia("(prefers-reduced-motion: reduce)").matches;

  function startCrowd() {
    const canvas = document.getElementById("crowd");
    if (!canvas) return;
    const ctx = canvas.getContext("2d");
    const img = new Image();
    img.src = SPRITE.src;

    // Three depth bands, like the logo: far (small, slow), mid, near (larger, rarer).
    const bands = [
      { y: 0.34, scale: 0.26, speed: 26, count: 5 },
      { y: 0.62, scale: 0.34, speed: 34, count: 5 },
      { y: 0.93, scale: 0.44, speed: 44, count: 3 },
    ];
    let people = [];
    let width = 0, height = 0, dpr = 1;
    let seed = 7;
    const rand = () => ((seed = (seed * 16807) % 2147483647) / 2147483647);

    function resize() {
      dpr = Math.min(window.devicePixelRatio || 1, 2);
      const r = canvas.getBoundingClientRect();
      width = r.width; height = r.height;
      canvas.width = Math.round(width * dpr);
      canvas.height = Math.round(height * dpr);
      const unit = Math.min(1, width / 1000);
      people = [];
      bands.forEach((b) => {
        for (let i = 0; i < b.count; i++) {
          const dir = rand() < 0.55 ? 1 : -1;
          people.push({
            x: rand() * width,
            y: b.y,
            scale: b.scale * (0.9 + rand() * 0.2) * Math.max(0.55, unit),
            speed: b.speed * (0.8 + rand() * 0.45) * Math.max(0.6, unit),
            dir,
            phase: rand() * SPRITE.frames,
            stride: 0.9 + rand() * 0.25,   // individual cadence
          });
        }
      });
      people.sort((a, b) => a.scale - b.scale);  // far first
    }

    function draw(dt) {
      ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
      ctx.clearRect(0, 0, width, height);
      for (const p of people) {
        const w = SPRITE.w * p.scale * 2.2, h = SPRITE.h * p.scale * 2.2;
        p.x += p.dir * p.speed * dt;
        if (p.x > width + w) p.x = -w;
        if (p.x < -w) p.x = width + w;
        // Legs advance with distance travelled, so feet never skate.
        p.phase = (p.phase + (p.speed * dt) / (w * 0.55) * p.stride * SPRITE.frames / 2) % SPRITE.frames;
        const frame = Math.floor(p.phase);
        const top = p.y * height - h;
        ctx.save();
        if (p.dir < 0) {
          ctx.translate(p.x + w / 2, 0);
          ctx.scale(-1, 1);
          ctx.translate(-(p.x + w / 2), 0);
        }
        ctx.drawImage(img, frame * SPRITE.w, 0, SPRITE.w, SPRITE.h, p.x - w / 2, top, w, h);
        ctx.restore();
      }
    }

    let last = 0, visible = true, raf = 0;
    function loop(t) {
      const dt = last ? Math.min(0.05, (t - last) / 1000) : 0;
      last = t;
      draw(dt);
      if (visible && !reducedMotion) raf = requestAnimationFrame(loop);
    }

    img.onload = () => {
      resize();
      draw(0);
      if (!reducedMotion) raf = requestAnimationFrame(loop);
    };
    window.addEventListener("resize", () => { resize(); draw(0); });
    new IntersectionObserver(([entry]) => {
      visible = entry.isIntersecting;
      if (visible && !reducedMotion) { last = 0; cancelAnimationFrame(raf); raf = requestAnimationFrame(loop); }
    }).observe(canvas);
  }

  // ---------------------------------------------------------------- answers
  // Verbatim MockDialogueProvider output (see unity/.../Editor/DemoVerify.cs log).
  const ANSWERS = [
    { name: "Ravi", role: "Taxi driver · impatient, sarcastic", emotion: "annoyed",
      text: "Right. Too many cars. Not enough patience. I fit right in." },
    { name: "Maya", role: "Barista · cheerful, curious", emotion: "happy",
      text: "Ooh, okay! I love it! Everyone's in a hurry but everyone's going somewhere, you know? Come by the café sometime!" },
    { name: "Daniel", role: "Retired train conductor · calm, precise", emotion: "thoughtful",
      text: "It has become faster and louder. But it still runs, and I respect anything that still runs." },
    { name: "Anjali", role: "Architect · analytical, busy", emotion: "neutral",
      text: "Structurally? Fine. Human-wise? It needs more trees and fewer car parks." },
    { name: "Arjun", role: "Student & guitarist · dreamy", emotion: "happy",
      text: "The city's like a song with too many instruments. Messy, but it slaps. That's a lyric, hold on." },
    { name: "Priya", role: "Night-shift nurse · kind, blunt", emotion: "tired",
      text: "Mm. It's a good city. I just mostly see it at 3 a.m., when it's bleeding a bit." },
  ];

  function renderAnswers() {
    const list = document.getElementById("answers");
    if (!list) return;
    for (const a of ANSWERS) {
      const li = document.createElement("li");
      li.className = "answer";
      li.innerHTML = `<span class="answer__name"></span><p class="answer__bubble"></p><p class="answer__meta"></p>`;
      li.querySelector(".answer__name").textContent = a.name;
      li.querySelector(".answer__bubble").textContent = a.text;
      li.querySelector(".answer__meta").textContent = `${a.role} · emotion: ${a.emotion}`;
      list.appendChild(li);
    }
  }

  // ------------------------------------------------------------------ unity
  const DEMO_ROOT = "demo/";
  const player = document.getElementById("player");
  const canvas = document.getElementById("unity-canvas");
  const playBtn = document.getElementById("play");
  const heroPlay = document.getElementById("hero-play");
  const fullscreenBtn = document.getElementById("fullscreen");
  const fill = document.getElementById("progress-fill");
  const bar = document.getElementById("progress");
  const label = document.getElementById("progress-label");
  const errorDetail = document.getElementById("error-detail");
  let unity = null;
  let started = false;

  const setState = (s) => { player.dataset.state = s; };

  function fail(message) {
    setState("error");
    if (message) errorDetail.textContent = message;
    console.error("[OpenNPC]", message);
  }

  function loadScript(src) {
    return new Promise((resolve, reject) => {
      const s = document.createElement("script");
      s.src = src;
      s.onload = resolve;
      s.onerror = () => reject(new Error("Could not load " + src));
      document.body.appendChild(s);
    });
  }

  async function startDemo() {
    if (started) return;
    started = true;
    if (location.protocol === "file:") {
      fail("Browsers block WebGL builds opened from disk. Serve the folder instead: python -m http.server -d web  →  http://localhost:8000");
      return;
    }
    setState("loading");

    // build.json is written by the Unity deploy step and names the actual build files,
    // so this page never has to guess compression suffixes.
    let manifest;
    try {
      const res = await fetch(DEMO_ROOT + "build.json", { cache: "no-cache" });
      if (!res.ok) throw new Error(res.status + " " + res.statusText);
      manifest = await res.json();
    } catch (e) {
      fail();
      return;
    }

    const build = DEMO_ROOT + "Build/";
    try {
      await loadScript(build + manifest.loaderUrl);
    } catch (e) {
      fail(e.message);
      return;
    }

    // The query string passes straight through to Unity: ?provider=opennpc&endpoint=https://…
    const config = {
      dataUrl: build + manifest.dataUrl,
      frameworkUrl: build + manifest.frameworkUrl,
      codeUrl: build + manifest.codeUrl,
      streamingAssetsUrl: DEMO_ROOT + "StreamingAssets",
      companyName: "OpenNPC",
      productName: "OpenNPC Demo",
      productVersion: manifest.version || "0.1.0",
      matchWebGLToCanvasSize: true,
      devicePixelRatio: Math.min(window.devicePixelRatio || 1, 2),
    };

    try {
      unity = await window.createUnityInstance(canvas, config, (p) => {
        const pct = Math.round(p * 100);
        fill.style.width = pct + "%";
        bar.setAttribute("aria-valuenow", String(pct));
        label.textContent = pct + "%";
      });
      setState("running");
      fullscreenBtn.disabled = false;
      canvas.focus();
    } catch (e) {
      fail("Unity failed to start: " + e);
    }
  }

  playBtn.addEventListener("click", startDemo);
  heroPlay.addEventListener("click", () => startDemo());
  fullscreenBtn.addEventListener("click", () => unity && unity.SetFullscreen(1));
  canvas.addEventListener("click", () => canvas.focus());

  // Let TAB / arrows / space reach Unity while the canvas has focus instead of scrolling the page.
  canvas.addEventListener("keydown", (e) => {
    if (["Tab", "ArrowUp", "ArrowDown", "ArrowLeft", "ArrowRight"].includes(e.key)) e.preventDefault();
  });
  // Space is blocked on keypress, not keydown: cancelling keydown also cancels the
  // keypress that Unity's text fields read characters from, so typed spaces vanished.
  // Browsers scroll on space during keypress, so this still stops the page jumping.
  canvas.addEventListener("keypress", (e) => {
    if (e.key === " ") e.preventDefault();
  });

  const repo = document.getElementById("repo-link");
  if (repo && document.documentElement.dataset.repo) repo.href = document.documentElement.dataset.repo;

  renderAnswers();
  startCrowd();
})();

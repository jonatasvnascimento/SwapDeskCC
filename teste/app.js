// Estado simulado — no app real isso vem do arquivo de configuração do SwapDeskCC
const state = {
  platform: "windows",
  scope: "both",
  activeId: "pessoal",
  swapping: false,
  newColor: "#2e7d32",
  accounts: [
    { id: "pessoal", name: "Pessoal", email: "voce@pessoal.com", plan: "Pro", color: "#c96442" },
    { id: "trabalho", name: "Trabalho", email: "voce@empresa.com", plan: "Max", color: "#3367d6" },
  ],
  log: [],
};

const COLORS = ["#c96442", "#3367d6", "#2e7d32", "#8e44ad", "#d81b60", "#00897b"];

const PATHS = {
  windows: {
    exe: "C:\\Program Files\\WindowsApps\\Claude_4.29.0.0_x64__pzs8sxrjxfjjc\\app\\Claude.exe",
    data: "%APPDATA%\\Claude",
    profiles: "%APPDATA%\\SwapDeskCC\\profiles",
    code: "%USERPROFILE%\\.claude-",
    link: "junction",
    desktopSupport: "ok",
  },
  linux: {
    exe: "/usr/bin/claude-desktop (build não oficial)",
    data: "~/.config/Claude",
    profiles: "~/.local/share/swapdeskcc/profiles",
    code: "~/.claude-",
    link: "symlink",
    desktopSupport: "experimental",
  },
};

const $ = (s) => document.querySelector(s);
const active = () => state.accounts.find((a) => a.id === state.activeId);
const initials = (n) => n.split(/\s+/).map((p) => p[0]).join("").slice(0, 2).toUpperCase();
const now = () => new Date().toLocaleTimeString("pt-BR", { hour: "2-digit", minute: "2-digit" });
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

function scopeLabel(scope) {
  return { both: "Desktop + Code", desktop: "Só Desktop", code: "Só Code" }[scope];
}

function addLog(text) {
  state.log.unshift({ t: now(), text });
  state.log = state.log.slice(0, 6);
}

// ---------- Render ----------

function render() {
  const p = PATHS[state.platform];
  const a = active();

  $("#activeBanner").innerHTML = `
    <span class="dot" style="background:${a.color}"></span>
    <span>Ativa agora: <b>${a.name}</b> <span class="muted">· ${a.email}</span></span>
    <span class="targets">
      <span class="pill ${p.desktopSupport === "ok" ? "ok" : "warn"}">Desktop ${p.desktopSupport === "ok" ? "✓" : "experimental"}</span>
      <span class="pill ok">Code ✓</span>
    </span>`;

  $("#cards").innerHTML = state.accounts.map((acc) => {
    const isActive = acc.id === state.activeId;
    return `
      <div class="card ${isActive ? "active" : ""}" style="--c:${acc.color}">
        ${isActive ? `<span class="active-tag">● ATIVA</span>` : ""}
        <div class="card-top">
          <div class="avatar">${initials(acc.name)}</div>
          <div>
            <div class="card-name">${acc.name}</div>
            <div class="card-email">${acc.email || "—"}</div>
          </div>
        </div>
        <div class="badges">
          <span class="pill">${acc.plan}</span>
          <span class="pill">Code: ${p.code}${acc.id}</span>
        </div>
        <button class="btn ${isActive ? "" : "primary"}" data-swap="${acc.id}" ${isActive || state.swapping ? "disabled" : ""}>
          ${isActive ? "Em uso" : "Usar esta conta"}
        </button>
      </div>`;
  }).join("");

  $("#log").innerHTML = state.log.length
    ? state.log.map((l) => `<li><time>${l.t}</time><span>${l.text}</span></li>`).join("")
    : `<li class="muted">Nenhuma troca ainda.</li>`;

  $("#trayDot").style.background = a.color;
  $("#detectedPath").textContent = p.exe;
  $("#dataPath").textContent = `${p.data} → ${p.profiles}\\${a.id}`.replace(/\\/g, state.platform === "linux" ? "/" : "\\");
  $("#methodPill").textContent = p.link;

  renderTray();
}

function renderTray() {
  $("#trayMenu").innerHTML = `
    <div class="hdr">SwapDeskCC · trocar ${scopeLabel(state.scope)}</div>
    ${state.accounts.map((acc) => `
      <button class="mi" data-swap="${acc.id}">
        <span class="check">${acc.id === state.activeId ? "✓" : ""}</span>
        <span class="d" style="background:${acc.color}"></span>
        ${acc.name}
      </button>`).join("")}
    <div class="sep"></div>
    <button class="mi" data-action="open"><span class="check"></span>Abrir janela</button>
    <button class="mi" data-action="quit"><span class="check"></span>Sair</button>`;
}

// ---------- Troca simulada ----------

function buildSteps(target) {
  const p = PATHS[state.platform];
  const win = state.platform === "windows";
  const steps = [];
  const desktop = state.scope !== "code";
  const code = state.scope !== "desktop";

  if (desktop) {
    steps.push({
      text: "Fechando Claude Desktop",
      note: win ? "11 processos em WindowsApps\\Claude_* (Claude Code não é afetado)" : "pkill pelo caminho do executável",
      ms: 900,
    });
    steps.push({ text: "Aguardando liberar arquivos", note: "", ms: 700 });
    steps.push({ text: "Backup de segurança", note: `${p.profiles}/${state.activeId}.bak`, ms: 600 });
    steps.push({ text: `Apontando ${p.data} para "${target.name}"`, note: `via ${p.link}`, ms: 700 });
  }
  if (code) {
    steps.push({
      text: "Claude Code: trocando CLAUDE_CONFIG_DIR",
      note: `${p.code}${target.id}  (vale para terminais novos)`,
      ms: 600,
    });
  }
  if (desktop) {
    steps.push({ text: "Abrindo Claude Desktop", note: win ? "shell:AppsFolder\\Claude_pzs8sxrjxfjjc!App" : "", ms: 900 });
  }
  return steps;
}

async function swap(id) {
  if (state.swapping || id === state.activeId) return;
  const target = state.accounts.find((a) => a.id === id);
  const from = active();
  state.swapping = true;
  closeTray();

  const steps = buildSteps(target);
  $("#swapTitle").textContent = `Trocando para ${target.name}…`;
  $("#steps").innerHTML = steps.map((s) => `
    <li><span class="ic">○</span><span>${s.text}${s.note ? `<small>${s.note}</small>` : ""}</span></li>`).join("");
  $("#progressBar").style.width = "0";
  $("#overlay").classList.add("on");

  const items = [...document.querySelectorAll("#steps li")];
  for (let i = 0; i < steps.length; i++) {
    items[i].classList.add("run");
    items[i].querySelector(".ic").textContent = "◌";
    await sleep(steps[i].ms);
    items[i].classList.replace("run", "done");
    items[i].querySelector(".ic").textContent = "✓";
    $("#progressBar").style.width = `${((i + 1) / steps.length) * 100}%`;
  }
  await sleep(400);
  $("#overlay").classList.remove("on");

  state.activeId = id;
  state.swapping = false;
  addLog(`${from.name} → <b>${target.name}</b> <span class="muted">(${scopeLabel(state.scope)})</span>`);
  render();
  toast(target);
}

let toastTimer;
function toast(acc) {
  const el = $("#toast");
  el.innerHTML = `
    <div class="avatar" style="--c:${acc.color};width:34px;height:34px;font-size:13px">${initials(acc.name)}</div>
    <div><b>Conta trocada: ${acc.name}</b><small>${scopeLabel(state.scope)} já usando ${acc.email || acc.name}</small></div>`;
  el.classList.add("on");
  clearTimeout(toastTimer);
  toastTimer = setTimeout(() => el.classList.remove("on"), 3500);
}

// ---------- Tray / janela ----------

function closeTray() { $("#trayMenu").classList.remove("on"); }
function showWindow(show) { $("#window").classList.toggle("hidden", !show); }

// ---------- Eventos ----------

document.addEventListener("click", (e) => {
  const swapBtn = e.target.closest("[data-swap]");
  if (swapBtn) return swap(swapBtn.dataset.swap);

  const action = e.target.closest("[data-action]");
  if (action) {
    closeTray();
    if (action.dataset.action === "open") showWindow(true);
    if (action.dataset.action === "quit") alert("No app real: encerra o SwapDeskCC (o Claude continua aberto).");
    return;
  }

  if (e.target.closest("#trayBtn")) return $("#trayMenu").classList.toggle("on");
  if (!e.target.closest("#trayMenu")) closeTray();
});

$("#closeWin").onclick = () => showWindow(false); // fecha para a bandeja
$("#tbApp").onclick = () => showWindow(true);

document.querySelectorAll(".nav").forEach((btn) => {
  btn.onclick = () => {
    document.querySelectorAll(".nav").forEach((b) => b.classList.toggle("on", b === btn));
    document.querySelectorAll(".page").forEach((p) => p.classList.toggle("on", p.id === `page-${btn.dataset.page}`));
  };
});

function bindSeg(sel, key, apply) {
  document.querySelectorAll(`${sel} button`).forEach((btn) => {
    btn.onclick = () => {
      document.querySelectorAll(`${sel} button`).forEach((b) => b.classList.toggle("on", b === btn));
      apply(btn.dataset[key]);
    };
  });
}

bindSeg("#platformSeg", "platform", (v) => { state.platform = v; document.documentElement.dataset.platform = v; render(); tick(); });
bindSeg("#themeSeg", "theme", (v) => { document.documentElement.dataset.theme = v; });
bindSeg("#scopeSeg", "scope", (v) => { state.scope = v; renderTray(); });

// Adicionar conta
$("#addBtn").onclick = () => {
  $("#swatches").innerHTML = COLORS.map((c) =>
    `<button type="button" style="background:${c}" data-color="${c}" class="${c === state.newColor ? "on" : ""}"></button>`).join("");
  $("#addOverlay").classList.add("on");
};
$("#swatches").onclick = (e) => {
  const b = e.target.closest("[data-color]");
  if (!b) return;
  state.newColor = b.dataset.color;
  document.querySelectorAll("#swatches button").forEach((x) => x.classList.toggle("on", x === b));
};
$("#cancelAdd").onclick = () => $("#addOverlay").classList.remove("on");
$("#addForm").onsubmit = (e) => {
  e.preventDefault();
  const f = new FormData(e.target);
  const name = f.get("name").trim();
  const id = name.toLowerCase().normalize("NFD").replace(/[^a-z0-9]/g, "") || `conta${state.accounts.length + 1}`;
  state.accounts.push({ id, name, email: f.get("email").trim(), plan: "—", color: state.newColor });
  addLog(`Conta <b>${name}</b> adicionada`);
  e.target.reset();
  $("#addOverlay").classList.remove("on");
  render();
};

// Relógio
function tick() {
  const d = new Date();
  const time = d.toLocaleTimeString("pt-BR", { hour: "2-digit", minute: "2-digit" });
  $("#clock").innerHTML = state.platform === "linux"
    ? `${d.toLocaleDateString("pt-BR", { weekday: "short", day: "numeric", month: "short" })}  ${time}`
    : `${time}<br>${d.toLocaleDateString("pt-BR")}`;
}
setInterval(tick, 10000);
tick();

document.documentElement.dataset.platform = "windows";
render();

// HelpDesk Tracker - front end
// Plain JavaScript (no framework). Talks to the C# Web API with fetch().
"use strict";

const STATUSES = ["New", "Assigned", "InProgress", "OnHold", "Resolved", "Closed"];
const PRIORITIES = ["Critical", "High", "Medium", "Low"];

const state = {
  lookups: { categories: [], users: [] },
  incidents: [],
  selectedId: null,
};

const $ = (id) => document.getElementById(id);

// ---------------- API helper ----------------

async function api(path, options = {}) {
  const response = await fetch(`/api/${path}`, {
    headers: { "Content-Type": "application/json" },
    ...options,
    body: options.body ? JSON.stringify(options.body) : undefined,
  });

  if (!response.ok) {
    // The API returns ProblemDetails JSON; validation errors include an "errors" map
    let message = `Request failed (${response.status})`;
    try {
      const problem = await response.json();
      if (problem.errors) message = Object.values(problem.errors).flat().join(" ");
      else if (problem.detail) message = problem.detail;
      else if (problem.title) message = problem.title;
    } catch { /* body was not JSON */ }
    throw new Error(message);
  }
  return response.status === 204 ? null : response.json().catch(() => null);
}

// ---------------- Small DOM helpers (textContent keeps user input safe from HTML injection) ----------------

function el(tag, props = {}, ...children) {
  const node = document.createElement(tag);
  Object.assign(node, props);
  for (const child of children) {
    if (child !== null && child !== undefined) node.append(child);
  }
  return node;
}

function badge(text, cssClass) {
  return el("span", { className: `badge ${cssClass}`, textContent: label(text) });
}

function label(value) {
  return value === "InProgress" ? "In progress" : value === "OnHold" ? "On hold" : value;
}

function fillSelect(select, items, getValue, getText, keepFirst = true) {
  const first = keepFirst ? select.options[0] : null;
  select.replaceChildren(...(first ? [first] : []),
    ...items.map((item) => el("option", { value: getValue(item), textContent: getText(item) })));
}

function formatDate(iso) {
  return new Date(iso).toLocaleString(undefined, { dateStyle: "medium", timeStyle: "short" });
}

function currentUserId() {
  return Number($("currentUser").value);
}

let toastTimer;
function toast(message, isError = false) {
  const t = $("toast");
  t.textContent = message;
  t.className = isError ? "toast error" : "toast";
  t.hidden = false;
  clearTimeout(toastTimer);
  toastTimer = setTimeout(() => { t.hidden = true; }, isError ? 6000 : 3000);
}

// ---------------- Loading data ----------------

async function loadLookups() {
  state.lookups = await api("lookups");
  const agents = state.lookups.users.filter((u) => u.isSupportAgent);

  fillSelect($("currentUser"), state.lookups.users, (u) => u.userId,
    (u) => u.fullName + (u.isSupportAgent ? " (support)" : ""), false);
  if (agents.length) $("currentUser").value = agents[0].userId;

  fillSelect($("assignTo"), agents, (u) => u.userId, (u) => u.fullName);
  fillSelect($("nCategory"), state.lookups.categories, (c) => c.categoryId,
    (c) => `${c.applicationName} – ${c.name}`, false);
}

async function loadDashboard() {
  const s = await api("dashboard");
  $("statOpen").textContent = s.openCount;
  $("statCritical").textContent = s.criticalOpen;
  $("statUnassigned").textContent = s.unassignedCount;
  $("statResolved").textContent = s.resolvedLast7Days;
  $("statAvg").textContent = s.avgHoursToResolve ?? "–";
}

function currentFilters() {
  const params = new URLSearchParams();
  if ($("statusFilter").value) params.set("status", $("statusFilter").value);
  if ($("priorityFilter").value) params.set("priority", $("priorityFilter").value);
  if ($("mineOnly").checked) params.set("assignedToId", currentUserId());
  const search = $("searchText").value.trim();
  if (search) params.set("search", search);
  return params;
}

async function loadIncidents() {
  try {
    state.incidents = await api(`incidents?${currentFilters()}`);
    renderIncidents();
  } catch (err) {
    $("incidentRows").replaceChildren(
      el("tr", {}, el("td", { colSpan: 7, className: "empty", textContent: err.message })));
  }
}

// ---------------- Rendering ----------------

function slaPrefix(incident) {
  if (!incident.isOverdue) return "";
  const done = incident.status === "Resolved" || incident.status === "Closed";
  return done ? "SLA missed · " : "Overdue · ";
}

function renderIncidents() {
  const rows = state.incidents.map((i) => {
    const tr = el("tr", { className: i.id === state.selectedId ? "selected" : "" },
      el("td", { className: "num", textContent: i.id }),
      el("td", { className: "title", textContent: i.title }),
      el("td", {}, badge(i.priority, `p-${i.priority}`)),
      el("td", {}, badge(i.status, `s-${i.status}`)),
      el("td", { textContent: i.applicationName }),
      el("td", { textContent: i.assignedToName ?? "—" }),
      el("td", { className: i.isOverdue ? "overdue" : "",
        textContent: slaPrefix(i) + formatDate(i.slaDueAt) }));
    tr.addEventListener("click", () => openDetail(i.id));
    return tr;
  });

  $("incidentRows").replaceChildren(...(rows.length ? rows
    : [el("tr", {}, el("td", { colSpan: 7, className: "empty", textContent: "No incidents match these filters." }))]));
}

async function openDetail(id) {
  try {
    const { incident: inc, isOverdue, nextStatuses } = await api(`incidents/${id}`);
    state.selectedId = id;
    renderIncidents();

    $("dId").textContent = `Incident #${inc.id}`;
    $("dTitle").textContent = inc.title;
    $("dBadges").replaceChildren(...[
      badge(inc.priority, `p-${inc.priority}`),
      badge(inc.status, `s-${inc.status}`),
      isOverdue ? badge("SLA missed", "p-Critical") : null,
    ].filter(Boolean));
    $("dApp").textContent = `${inc.applicationName} – ${inc.categoryName}`;
    $("dReporter").textContent = `${inc.reportedByName} (${inc.reportedByDepartment})`;
    $("dAssignee").textContent = inc.assignedToName ?? "Unassigned";
    $("dCreated").textContent = formatDate(inc.createdAt);
    $("dSla").textContent = formatDate(inc.slaDueAt);
    $("dDescription").textContent = inc.description;

    $("dResolutionBlock").hidden = !inc.resolution;
    $("dRootCause").textContent = inc.rootCause ?? "";
    $("dResolution").textContent = inc.resolution ?? "";

    // Only offer the status changes the workflow allows
    const statusForm = $("statusForm");
    statusForm.hidden = nextStatuses.length === 0;
    fillSelect($("newStatus"), nextStatuses, (s) => s, label, false);
    $("assignTo").value = "";
    $("rootCause").value = inc.rootCause ?? "";
    $("resolution").value = inc.resolution ?? "";
    toggleResolveFields();

    $("dNotes").replaceChildren(...(inc.notes.length ? inc.notes.map((n) =>
      el("li", { className: n.isInternal ? "" : "public" },
        el("div", { className: "meta",
          textContent: `${n.authorName} · ${formatDate(n.createdAt)}${n.isInternal ? " · internal" : " · shared with user"}` }),
        el("p", { textContent: n.noteText })))
      : [el("li", { className: "meta", textContent: "No notes yet." })]));
    $("noteForm").hidden = inc.status === "Closed";

    $("detailPanel").hidden = false;
    document.querySelector(".layout").classList.add("with-detail");
  } catch (err) {
    toast(err.message, true);
  }
}

function closeDetail() {
  state.selectedId = null;
  $("detailPanel").hidden = true;
  document.querySelector(".layout").classList.remove("with-detail");
  renderIncidents();
}

function toggleResolveFields() {
  $("resolveFields").hidden = $("newStatus").value !== "Resolved";
}

async function refreshAll() {
  await Promise.all([loadIncidents(), loadDashboard()]);
  if (state.selectedId) await openDetail(state.selectedId);
}

// ---------------- Actions ----------------

async function withBusy(form, action) {
  const button = form.querySelector("button[type=submit]");
  button.disabled = true;
  try { await action(); }
  catch (err) { toast(err.message, true); }
  finally { button.disabled = false; }
}

async function submitStatus(event) {
  event.preventDefault();
  await withBusy(event.target, async () => {
    const newStatus = $("newStatus").value;
    const isResolving = newStatus === "Resolved";
    await api(`incidents/${state.selectedId}/status`, {
      method: "PUT",
      body: {
        newStatus,
        assignedToId: $("assignTo").value ? Number($("assignTo").value) : null,
        rootCause: isResolving ? $("rootCause").value : null,
        resolution: isResolving ? $("resolution").value : null,
        changedById: currentUserId(),
      },
    });
    toast(`Incident moved to ${label(newStatus)}.`);
    await refreshAll();
  });
}

async function submitNote(event) {
  event.preventDefault();
  await withBusy(event.target, async () => {
    await api(`incidents/${state.selectedId}/notes`, {
      method: "POST",
      body: { authorId: currentUserId(), noteText: $("noteText").value, isInternal: $("noteInternal").checked },
    });
    $("noteText").value = "";
    toast("Note added.");
    await openDetail(state.selectedId);
  });
}

async function submitNewIncident(event) {
  event.preventDefault();
  await withBusy(event.target, async () => {
    const result = await api("incidents", {
      method: "POST",
      body: {
        title: $("nTitle").value,
        description: $("nDescription").value,
        categoryId: Number($("nCategory").value),
        priority: $("nPriority").value,
        reportedById: currentUserId(),
      },
    });
    $("newDialog").close();
    $("newForm").reset();
    toast(`Incident #${result.id} created.`);
    await refreshAll();
    await openDetail(result.id);
  });
}

function exportXml() {
  // Let the browser download the file directly from the API
  window.location.href = `/api/incidents/export.xml?${currentFilters()}`;
}

// ---------------- Start up ----------------

function debounce(fn, ms) {
  let timer;
  return (...args) => { clearTimeout(timer); timer = setTimeout(() => fn(...args), ms); };
}

async function init() {
  fillSelect($("statusFilter"), STATUSES, (s) => s, label);
  fillSelect($("priorityFilter"), PRIORITIES, (p) => p, (p) => p);
  fillSelect($("nPriority"), PRIORITIES, (p) => p, (p) => p, false);
  $("nPriority").value = "Medium";

  $("filterForm").addEventListener("submit", (e) => { e.preventDefault(); loadIncidents(); });
  $("searchText").addEventListener("input", debounce(loadIncidents, 300));
  for (const id of ["statusFilter", "priorityFilter", "mineOnly"]) $(id).addEventListener("change", loadIncidents);
  $("currentUser").addEventListener("change", () => { if ($("mineOnly").checked) loadIncidents(); });

  $("exportBtn").addEventListener("click", exportXml);
  $("newBtn").addEventListener("click", () => $("newDialog").showModal());
  $("cancelNew").addEventListener("click", () => $("newDialog").close());
  $("newForm").addEventListener("submit", submitNewIncident);
  $("closeDetail").addEventListener("click", closeDetail);
  $("newStatus").addEventListener("change", toggleResolveFields);
  $("statusForm").addEventListener("submit", submitStatus);
  $("noteForm").addEventListener("submit", submitNote);

  try {
    await loadLookups();
    await refreshAll();
  } catch (err) {
    toast(`Could not reach the API: ${err.message}`, true);
  }
}

document.addEventListener("DOMContentLoaded", init);

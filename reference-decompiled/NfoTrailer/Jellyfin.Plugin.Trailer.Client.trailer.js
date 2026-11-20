(function () {
"use strict";
if (window.__nfoTrailerInstalled) return;
window.__nfoTrailerInstalled = true;

const cache = new Map();
function activeClient() {
    return window.ApiClient ||
        window.Dashboard?.getCurrentApiClient?.() ||
        window.connectionManager?.currentApiClient?.() ||
        window.ConnectionManager?.currentApiClient?.();
}
window.NfoTrailerDebug = () => ({loaded:true, url:location.href, movieId:movieId(), apiRoot:apiRoot(), currentButton:!!document.querySelector("[data-nfo-trailer]"), cached:[...cache.entries()], errors:[...checked.entries()], activeRoute, pending, visibleActionTargets:[...document.querySelectorAll(ACTIONS_SELECTOR)].filter(visible).length});
let pendingId = null;
let pending = false;
const checked = new Map();
function apiRoot() {
    const client = activeClient();
    const server = typeof client?.serverAddress === "function" ? client.serverAddress()
        : (typeof client?.serverAddress === "string" ? client.serverAddress : null);
    if (window.tizen && server) return server.replace(/\/$/, "") + "/NfoTrailer/";
    return location.pathname.replace(/\/(?:web\/?)?(?:index\.html)?$/i, "") + "/NfoTrailer/";
}

function movieId() {
    const route = location.hash || "";
    if (!/(?:^|[#!/])details(?:\?|\/|$)/i.test(route)) return null;
    const match = route.match(/[?&]id=([a-f0-9]{32}|[a-f0-9-]{36})(?=&|$)/i);
    return match ? match[1] : null;
}
function close() {
    document.getElementById("nfo-trailer-dialog")?.remove();
    document.removeEventListener("keydown",onKey,true);
}
function onKey(e) {
    if (e.key === "Escape" || e.key === "Backspace" || e.key === "BrowserBack" ||
        e.key === "XF86Back" || e.keyCode === 10009) {
        e.preventDefault();
        e.stopImmediatePropagation();
        close();
    }
}
function open(videoId) {
    if (!/^[A-Za-z0-9_-]{11}$/.test(videoId)) return;
    close();
    const overlay = document.createElement("div");
    overlay.id = "nfo-trailer-dialog";
    overlay.setAttribute("role","dialog");
    overlay.setAttribute("aria-modal","true");
    overlay.setAttribute("aria-label","Trailer");
    overlay.style.cssText = "position:fixed;inset:0;z-index:99999;background:rgba(0,0,0,.9);display:flex;align-items:center;justify-content:center;padding:18px";
    const shell = document.createElement("div");
    shell.style.cssText = "position:relative;width:min(100%,1000px);aspect-ratio:16/9;background:#000;border-radius:10px";
    const frame = document.createElement("iframe");
    frame.title = "Trailer YouTube";
    frame.src = "https://www.youtube-nocookie.com/embed/" + videoId + "?autoplay=1&rel=0";
    frame.allow = "autoplay;encrypted-media;picture-in-picture;fullscreen";
    frame.allowFullscreen = true;
    frame.referrerPolicy = "strict-origin-when-cross-origin";
    frame.style.cssText = "width:100%;height:100%;border:0;border-radius:10px";
    const dismiss = document.createElement("button");
    dismiss.type = "button";
    dismiss.textContent = "✕";
    dismiss.setAttribute("aria-label","Închide trailer");
    dismiss.style.cssText = "position:absolute;right:0;top:-48px;color:white;background:#353a44;border:0;border-radius:50%;width:42px;height:42px;font-size:22px;cursor:pointer";
    dismiss.addEventListener("click", close);
    overlay.addEventListener("click", e => { if (e.target === overlay) close(); });
    // Some Samsung Tizen WebViews cannot play embedded YouTube. Offer a
    // visible external-player fallback rather than a silent blank iframe.
    const fallback = document.createElement("a");
    fallback.href = "https://www.youtube.com/watch?v=" + encodeURIComponent(videoId);
    fallback.target = "_blank";
    fallback.rel = "noopener noreferrer";
    fallback.textContent = "Deschide pe YouTube";
    fallback.style.cssText = "position:absolute;bottom:-42px;left:0;color:#fff;font-size:16px;text-decoration:underline";
    shell.append(frame, dismiss, fallback);
    overlay.append(shell);
    document.body.append(overlay);
    dismiss.focus();
    document.addEventListener("keydown", onKey, true);
}
// Jellyfin Web is a SPA: old detail views can remain in the DOM while hidden.
// Always target the CURRENT visible detail page rather than the first match.
const BUTTON_SELECTOR = "[data-nfo-trailer]";
const ACTIONS_SELECTOR = ".mainDetailButtons,.detailPagePrimaryContainer .detailPageButtons,.detailPagePrimaryContainer .mainDetailButtons,.detailPageWrapperContainer .mainDetailButtons,.detailPagePrimaryContainer .detailButtons";
let requestGeneration = 0;
let activeRoute = null;
function visible(element) {
    if (!element || !element.isConnected || !element.getClientRects().length) return false;
    for (let node = element; node && node !== document.body; node = node.parentElement) {
        if (node.hidden || node.getAttribute("aria-hidden") === "true") return false;
        const style = window.getComputedStyle(node);
        if (style.display === "none" || style.visibility === "hidden") return false;
    }
    return true;
}
function actionsTarget() {
    const candidates = [...document.querySelectorAll(ACTIONS_SELECTOR)].filter(visible);
    return candidates[candidates.length - 1] || null;
}
function renderButton(id, videoId) {
    if (!videoId || movieId() !== id) return;
    const target = actionsTarget();
    if (!target) return;
    // Jellyfin may replace just the action buttons after the initial render.
    document.querySelectorAll(BUTTON_SELECTOR).forEach(button => {
        if (button.dataset.movieId !== id || !target.contains(button)) button.remove();
    });
    if (target.querySelector(BUTTON_SELECTOR)) return;
    const button = document.createElement("button");
    button.type = "button";
    button.className = "raised button-submit emby-button";
    button.dataset.nfoTrailer = "1";
    button.dataset.movieId = id;
    button.textContent = "▶ Trailer";
    button.style.cssText = "display:inline-flex;align-items:center;gap:8px;padding:10px 18px;border-radius:7px;cursor:pointer;margin:4px 8px 4px 0";
    button.addEventListener("click", () => open(videoId));
    target.append(button);
}
async function refresh() {
    const id = movieId();
    if (id !== activeRoute) {
        activeRoute = id;
        requestGeneration++;
        pending = false;
        pendingId = null;
        close();
        document.querySelectorAll(BUTTON_SELECTOR).forEach(el => el.remove());
    }
    if (!id) return;
    const target = actionsTarget();
    if (!target) return;
    const cached = cache.get(id);
    if (cached !== undefined) {
        renderButton(id, cached);
        return;
    }
    if (pending && pendingId === id) return;
    // Suppress repeated requests within one visit after a server failure,
    // but allow another attempt when the user navigates away and back.
    if (checked.get(id) === requestGeneration) return;
    pending = true;
    pendingId = id;
    const generation = requestGeneration;
    try {
        const client = activeClient();
        const token = client?.accessToken?.() || client?._serverInfo?.AccessToken;
        if (!token) return;
        const endpoint = apiRoot() + "Movie/" + encodeURIComponent(id);
        const authorization = 'MediaBrowser Client="NfoTrailer", Device="Jellyfin Web", DeviceId="nfo-trailer-web", Version="0.1", Token="' + token + '"';
        const response = await fetch(endpoint, {
            headers: { "Authorization": authorization, "X-Emby-Token": token },
            credentials: "same-origin", cache: "no-store"
        });
        if (generation !== requestGeneration || movieId() !== id) return;
        if (!response.ok) {
            checked.set(id, generation);
            console.warn("[NFO Trailer] HTTP", response.status, "for", id);
            return;
        }
        const result = await response.json();
        if (generation !== requestGeneration || movieId() !== id) return;
        const videoId = result.videoId || null;
        cache.set(id, videoId);
        if (videoId) renderButton(id, videoId);
        else console.info("[NFO Trailer] No valid NFO trailer:", result.reason || "not found", id);
    } catch (error) {
        if (generation === requestGeneration) {
            checked.set(id, generation);
            console.warn("[NFO Trailer] Request failed", error);
        }
    } finally {
        if (generation === requestGeneration) {
            pending = false;
            pendingId = null;
        }
    }
}
let queued = false;
function schedule() {
    if (queued || document.hidden) return;
    queued = true;
    requestAnimationFrame(() => { queued = false; refresh(); });
}
window.addEventListener("hashchange", schedule);
function start() {
    new MutationObserver(schedule).observe(document.body,{childList:true,subtree:true});
    schedule();
    window.addEventListener("pageshow",schedule);
    document.addEventListener("visibilitychange",schedule);
}
if (document.readyState === "loading") document.addEventListener("DOMContentLoaded",start,{once:true});
else start();
})();
(function(){
"use strict";
if(window.__requestMovieNavInstalled)return;
window.__requestMovieNavInstalled=true;
const rootId="request-movie-native";
function api(){return window.ApiClient||window.Dashboard?.getCurrentApiClient?.()||window.connectionManager?.currentApiClient?.()}
function url(){const c=api();return typeof c?.getUrl==="function"?c.getUrl("RequestMovie/Ui"):location.pathname.replace(/\/(?:web\/?)?(?:index\.html)?$/i,"")+"/RequestMovie/Ui"}
async function open(){
 if(document.getElementById(rootId)){document.getElementById(rootId).hidden=false;return}
 const c=api(),token=c?.accessToken?.()||c?._serverInfo?.AccessToken;
 const r=await fetch(url(),{credentials:"same-origin",cache:"no-store",headers:token?{"X-Emby-Token":token}:{}});
 if(!r.ok){console.warn("[Request Movie] UI",r.status);return}
 const html=await r.text(),panel=document.createElement("div");panel.id=rootId;
 panel.style.cssText="position:fixed;inset:0;overflow:auto;z-index:2200;background:#111826;color:white;padding:16px 12px 60px";
 const close=document.createElement("button");close.textContent="✕ Închide";close.style.cssText="display:block;margin:0 15px 10px auto;padding:12px;border-radius:8px;background:#455067;color:white;border:0";close.onclick=()=>panel.remove();panel.append(close);
 const host=document.createElement("div");host.innerHTML=html;const scripts=[...host.querySelectorAll("script")];scripts.forEach(x=>x.remove());panel.append(...host.childNodes);document.body.append(panel);
 scripts.forEach(s=>{const code=document.createElement("script");code.textContent=s.textContent;panel.append(code)});
}
function link(kind,text){const b=document.createElement("button");b.dataset.requestMovieLink=kind;b.type="button";b.textContent="🎬 "+text;b.style.cssText="padding:8px 13px;border-radius:8px;background:#353e52;color:white;border:0;cursor:pointer;margin:4px";b.onclick=e=>{e.preventDefault();open().catch(console.error)};return b}
function install(){
 const header=document.querySelector(".headerRight,.headerRightItems,.headerTop");
 if(header&&!header.querySelector('[data-request-movie-link="header"]'))header.prepend(link("header","Cereri"));
 const nav=document.querySelector(".mainDrawer-scrollContainer,.mainDrawer,.navMenuOptions");
 if(nav&&!nav.querySelector('[data-request-movie-link="drawer"]'))nav.append(link("drawer","Cereri filme"));
 const home=document.querySelector(".homePage");
 const native=home?.querySelector(".homeLibraryButton");
 if(native){const parent=native.parentElement;if(parent&&!parent.querySelector('[data-request-movie-link="home"]'))parent.prepend(link("home","Cereri"))}
}
function tick(){const p=document.getElementById(rootId);if(p&&location.hash&&!location.hash.includes("request-movie")){ /* leave user-controlled modal visible */ }install()}
function start(){new MutationObserver(()=>{if(!document.hidden)tick()}).observe(document.body,{childList:true,subtree:true});tick()}
if(document.readyState==="loading")document.addEventListener("DOMContentLoaded",start,{once:true});else start();
})();
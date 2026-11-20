(function(){
"use strict";
if(window.__requestMovieNavInstalled)return;
window.__requestMovieNavInstalled=true;
const ROOT_ID="request-movie-native";
const ICON='<svg aria-hidden="true" viewBox="0 0 24 24" width="20" height="20" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"><rect x="3" y="5" width="18" height="15" rx="2"/><path d="M3 10h18M7 5l3 5M14 5l3 5M9 14l3 2-3 2z"/></svg>';
const path=()=>location.pathname.replace(/\/(?:index\.html)?$/i,"");
function apiRoot(){const base=path();return base.replace(/\/web$/i,"")+"/RequestMovie/";}
const uiHash="#/request-movie";
function loggedIn(){const api=window.ApiClient||window.Dashboard?.getCurrentApiClient?.();try{return !!(api?.accessToken?.()||api?._serverInfo?.AccessToken)}catch{return false}}
function isDashboard(){return location.hash.indexOf("dashboard")>=0 || location.hash.indexOf("configuration")>=0}
let loading=false;
async function openPage(){
 if(loading)return;
 loading=true;
 try{
  let panel=document.getElementById(ROOT_ID);
  if(!panel){
   panel=document.createElement("div");panel.id=ROOT_ID;panel.style.cssText="position:fixed;inset:0;overflow:auto;background:#10151f;color:white;z-index:2000;padding:18px 12px 60px";
   const header=document.createElement("div");header.style.cssText="max-width:1120px;margin:auto;display:flex;justify-content:flex-end";
   const close=document.createElement("button");close.textContent="✕ Închide";close.style.cssText="background:#394259;border:0;border-radius:9px;color:white;padding:10px 14px;cursor:pointer";
   close.onclick=()=>{panel.remove()};
   header.append(close);panel.append(header);
   const api=window.ApiClient||window.Dashboard?.getCurrentApiClient?.();
   const token=api?.accessToken?.()||api?._serverInfo?.AccessToken;
   const response=await fetch(apiRoot()+"Ui",{headers:{
    Authorization:'MediaBrowser Client="RequestMovieNav", Device="Web", DeviceId="request-movie-nav", Version="0.2", Token="'+token+'"'
   },credentials:"same-origin",cache:"no-store"});
   if(!response.ok)throw Error("Nu s-a putut încărca formularul (HTTP "+response.status+")");
   const html=await response.text();
   const host=document.createElement("div");host.innerHTML=html;
   const scripts=[...host.querySelectorAll("script")];scripts.forEach(s=>s.remove());
   panel.append(...Array.from(host.childNodes));document.body.append(panel);
   // Execute the trusted embedded UI initializer after its markup has been attached.
   for(const script of scripts){const executable=document.createElement("script");executable.textContent=script.textContent;panel.append(executable)}
  }
  panel.hidden=false;
 }catch(err){window.alert("Cereri: "+err.message)}
 finally{loading=false}
}
function navLink(label,className){const a=document.createElement("a");a.href="#";a.className=className||"";a.dataset.requestMovieMenu="1";a.style.cssText="display:flex;align-items:center;gap:12px;color:inherit;text-decoration:none;cursor:pointer";a.innerHTML=ICON;const t=document.createElement("span");t.textContent=label;a.append(t);a.addEventListener("click",e=>{e.preventDefault();openPage()});return a}
function install(){
 // Android WebView may render authenticated Home before ApiClient becomes globally visible.
 if(!loggedIn()&&!document.querySelector(".homePage .homeLibraryButton"))return;
 // Jellyfin web header's main section.
 const header=document.querySelector(".headerRight,.headerRightItems,.headerTabs,.headerTop");
 if(header&&!header.querySelector('[data-request-movie-top="1"]')){
  const link=navLink("Cereri","request-movie-header-button");link.dataset.requestMovieTop="1";
  link.style.cssText+=";padding:8px 12px;border-radius:8px;font-weight:600";header.prepend(link)
 }
 // Jellyfin's side navigation around the My Media section.
 const menu=document.querySelector(".mainDrawer-scrollContainer,.mainDrawer,.navMenuOptions,.drawer-scrollContainer");
 if(menu&&!menu.querySelector('[data-request-movie-side="1"]')){
  const link=navLink("Cereri filme și seriale","navMenuOption");
  link.dataset.requestMovieSide="1";link.style.cssText+=";padding:13px 22px";
  const mediaTitle=[...menu.querySelectorAll("h2,h3,.navMenuOption,.sidebarHeader")].find(e=>/my media|media mea|bibliotecile mele|bibliotecile|my libraries/i.test(e.textContent||""));
  if(mediaTitle?.parentNode)mediaTitle.insertAdjacentElement("afterend",link);
  else menu.append(link)
 }
 // Inject Cereri as a *sibling of native library buttons*, not a child of
 // the outer cards container. This preserves layout, grid positioning and theme.
 const home=document.querySelector(".homePage");
 if(home){
  // Do not search by translated section headings: Android uses e.g.
  // "Fișierele mele Media", which differs from the desktop label.
  // Home library buttons are a stable anchor in both mobile and desktop Jellyfin Web.
  const native=home.querySelector("a.homeLibraryButton,button.homeLibraryButton,.homeLibraryButton");
  if(native){
   const reference=native.closest("a,button")||native;
   const parent=reference.parentElement;
   let tile=parent?.querySelector(':scope > [data-request-movie-home="1"]');
   if(!tile && parent){
    tile=reference.cloneNode(true);
    // Cloning the native element keeps the exact classes, wrapper, icon size,
    // dimensions, spacing and theme rules. Never add custom inline layout CSS.
    tile.dataset.requestMovieHome="1";
    tile.setAttribute("href","#/request-movie");
    tile.setAttribute("aria-label","Cereri");
    tile.removeAttribute("onclick");
    tile.removeAttribute("style");
    for(const node of [tile,...tile.querySelectorAll("*")]){
     node.removeAttribute("data-id");
     node.removeAttribute("data-item-id");
     node.removeAttribute("data-parent-id");
     node.removeAttribute("data-collection-type");
     node.removeAttribute("onclick");
     node.removeAttribute("id");
    }
    const icon=tile.querySelector(".material-icons,.material-icons-round,.material-symbols-outlined");
    if(icon)icon.textContent="movie";
    const labels=[...tile.querySelectorAll(".buttonText,.homeLibraryButtonText,.cardText,.itemName")];
    if(labels.length)labels[labels.length-1].textContent="Cereri";
    else{
     const textNodes=[];
     const walker=document.createTreeWalker(tile,NodeFilter.SHOW_TEXT);
     while(walker.nextNode()){
      const n=walker.currentNode;
      if(n.nodeValue.trim()&&!n.parentElement?.matches(".material-icons,.material-icons-round,.material-symbols-outlined"))
       textNodes.push(n);
     }
     if(textNodes.length)textNodes[textNodes.length-1].nodeValue="Cereri";
     else{const name=document.createElement("span");name.textContent="Cereri";tile.append(name)}
    }
    tile.addEventListener("click",function(event){
     event.preventDefault();
     event.stopImmediatePropagation();
     openPage();
    },true);
    reference.insertAdjacentElement("beforebegin",tile);
   }
  }
 }
 // The administrator view is rendered by Jellyfin separately; place a shortcut after plugin entries.
 if(isDashboard()){
  const container=document.querySelector(".dashboardSidebar,.sidebarLinks,.adminSidebar,.navMenuOptions");
  if(container&&!container.querySelector('[data-request-movie-admin="1"]')){
   const link=navLink("Cereri filme","navMenuOption");link.dataset.requestMovieAdmin="1";link.style.cssText+=";padding:12px 18px";
   const plugin=[...container.querySelectorAll("a")].find(e=>/plugins|pluginuri|extensii/i.test(e.textContent||""));
   if(plugin)plugin.insertAdjacentElement("afterend",link);else container.append(link);
  }
 }
}

function onHash(){
 const panel=document.getElementById(ROOT_ID);
 if(panel)panel.hidden=true;
 install()
}
window.addEventListener("hashchange",onHash);
const observer=new MutationObserver(()=>{if(!document.hidden)install()});
const start=()=>{observer.observe(document.body,{childList:true,subtree:true});install();onHash()};
if(document.readyState==="loading")document.addEventListener("DOMContentLoaded",start,{once:true});else start();
})();

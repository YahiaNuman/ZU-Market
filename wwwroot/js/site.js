document.addEventListener("click",event=>{const target=event.target.closest("[data-confirm]");if(target&&!window.confirm(target.dataset.confirm))event.preventDefault()});
document.querySelectorAll("[data-gallery-src]").forEach(button=>button.addEventListener("click",()=>{const image=document.getElementById("main-image");if(image)image.src=button.dataset.gallerySrc;document.querySelectorAll("[data-gallery-src]").forEach(item=>item.setAttribute("aria-pressed",String(item===button)))}));
document.querySelectorAll("form").forEach(form=>form.addEventListener("submit",()=>{form.setAttribute("aria-busy","true");form.querySelectorAll('button[type="submit"]').forEach(button=>button.classList.add("is-loading"))}));
const free=document.getElementById("listing-free");const price=document.getElementById("listing-price");
if(matchMedia("(prefers-reduced-motion: reduce)").matches)document.querySelectorAll("video[autoplay]").forEach(video=>{video.removeAttribute("autoplay");video.pause()});
let priceForced=false;function syncPrice(){if(!price)return;const forced=!!free?.checked;price.disabled=forced;if(forced)price.value="0";else if(priceForced)price.value="";priceForced=forced}
free?.addEventListener("change",syncPrice);syncPrice();

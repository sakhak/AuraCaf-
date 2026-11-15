(() => {
    "use strict";
    const config = window.auraPos;
    if (!config) return;
    const t = (text, values = {}) => {
        if (window.auraPosI18n) return window.auraPosI18n.t(text,values);
        return text.replace(/\{(\w+)\}/g,(match,name) => Object.hasOwn(values,name) ? String(values[name]) : match);
    };
    document.querySelectorAll("[data-pos-image]").forEach(img => {
        const fallback = () => { img.hidden = true; const icon = img.parentElement.querySelector(".pos-image-fallback"); if (icon) icon.hidden = false; };
        img.addEventListener("error", fallback);
        if (img.complete && img.naturalWidth === 0) fallback();
    });
    const products = new Map(config.products.map(product => [product.id, product]));
    const cards = Array.from(document.querySelectorAll(".pos-product-card"));
    const cart = new Map();
    const cartItems = document.getElementById("cartItems");
    const emptyCart = cartItems.firstElementChild.cloneNode(true);
    const cartTotal = document.getElementById("cartTotal");
    const cartJson = document.getElementById("cartJson");
    const checkoutButton = document.getElementById("checkoutButton");
    const checkoutForm = document.getElementById("checkoutForm");
    const clearCart = document.getElementById("clearCart");
    const search = document.getElementById("productSearch");
    const category = document.getElementById("categoryFilter");
    const categoryButtons = Array.from(document.querySelectorAll("[data-category-filter]"));
    const feedback = document.getElementById("posFeedback");
    const cartPanel = document.getElementById("posCart");
    const cartHost = document.getElementById("posCartHost");
    const cartDialog = document.getElementById("posCartDialog");
    const compact = window.matchMedia("(max-width: 1199.98px)");
    let submitting = false;
    const money = cents => new Intl.NumberFormat("en-US", { style: "currency", currency: "USD" }).format(cents / 100);
    document.getElementById("categoryButtons").hidden = false;
    document.querySelector(".pos-category-native").hidden = true;
    function syncCartPanel() {
        if (compact.matches) {
            if (cartPanel.parentElement !== cartDialog) cartDialog.appendChild(cartPanel);
        } else {
            if (cartDialog.open) cartDialog.close();
            cartHost.appendChild(cartPanel);
        }
    }
    compact.addEventListener("change", syncCartPanel);
    syncCartPanel();
    document.getElementById("openCart").addEventListener("click", () => { if (compact.matches && !cartDialog.open) cartDialog.showModal(); });
    document.getElementById("closeCart").addEventListener("click", () => cartDialog.close());
    cartDialog.addEventListener("close", () => { if (compact.matches) document.getElementById("openCart").focus(); });
    function addProduct(id) {
        if (!config.hasActiveShift || submitting || !products.has(id)) return;
        const quantity = cart.get(id) || 0;
        if (quantity >= 100) { window.auraDialog.notice({ title: "That's a full batch!", message: "You can add up to 100 units of each product." }); return; }
        if (!cart.has(id) && cart.size >= 50) { window.auraDialog.notice({ title: "Your tray is full", message: "An order can contain up to 50 different products." }); return; }
        cart.set(id,quantity + 1);
        renderCart();
        feedback.textContent = t("{name} added. {count} in your order.", { name: products.get(id).name, count: quantity + 1 });
    }
    function changeQuantity(id,delta) {
        if (submitting) return;
        const quantity = (cart.get(id) || 0) + delta;
        if (quantity > 100) return;
        if (quantity <= 0) cart.delete(id); else cart.set(id,quantity);
        renderCart();
        feedback.textContent = t(quantity > 0 ? "{name} quantity: {count}" : "{name} removed from the order.", { name: products.get(id).name, count: quantity });
    }
    function quantityButton(id,action,label,text,callback) {
        const button = document.createElement("button");
        button.type = "button";
        button.className = action === "remove" ? "pos-remove-item" : "pos-quantity-button";
        button.textContent = text;
        button.setAttribute("aria-label",label);
        button.dataset.cartAction = action;
        button.dataset.cartProduct = id;
        button.addEventListener("click",callback);
        return button;
    }
    function renderCart() {
        const active = document.activeElement;
        const focus = active?.dataset.cartAction ? { id: active.dataset.cartProduct, action: active.dataset.cartAction } : null;
        cartItems.replaceChildren();
        let totalCents = 0, units = 0;
        if (cart.size === 0) cartItems.appendChild(emptyCart.cloneNode(true));
        for (const [id,quantity] of cart) {
            const product = products.get(id);
            totalCents += product.priceCents * quantity;
            units += quantity;
            const item = document.createElement("div"); item.className = "pos-cart-item";
            const thumbnail = document.createElement("span"); thumbnail.className = "pos-cart-thumbnail"; thumbnail.setAttribute("aria-hidden","true"); thumbnail.textContent = "a.";
            if (product.imageUrl) {
                const img = document.createElement("img"); img.src = product.imageUrl; img.alt = ""; img.referrerPolicy = "no-referrer";
                img.addEventListener("error",() => img.remove()); thumbnail.appendChild(img);
            }
            const details = document.createElement("div"); details.className = "pos-cart-item-main";
            const name = document.createElement("strong"); name.textContent = product.name; name.setAttribute("data-pos-no-translate", "");
            const price = document.createElement("small"); price.textContent = t("{price} each", { price: money(product.priceCents) });
            const controls = document.createElement("div"); controls.className = "pos-quantity-controls";
            const minus = quantityButton(id,"decrease",t("Decrease {name}", { name: product.name }),"-",() => changeQuantity(id,-1));
            const count = document.createElement("span"); count.textContent = quantity;
            const plus = quantityButton(id,"increase",t("Increase {name}", { name: product.name }),"+",() => changeQuantity(id,1)); plus.disabled = quantity >= 100;
            controls.append(minus,count,plus);
            details.append(name,price,controls);
            const right = document.createElement("div"); right.className = "pos-cart-item-right";
            const subtotal = document.createElement("strong"); subtotal.textContent = money(product.priceCents * quantity);
            const remove = quantityButton(id,"remove",t("Remove {name}", { name: product.name }),t("Remove"),() => { if (!submitting) { cart.delete(id); renderCart(); } });
            right.append(subtotal,remove);
            item.append(thumbnail,details,right);
            cartItems.appendChild(item);
        }
        cartTotal.textContent = money(totalCents);
        document.getElementById("mobileCartTotal").textContent = money(totalCents);
        document.getElementById("cartItemCount").textContent = units;
        document.getElementById("mobileCartCount").textContent = units ? t(units === 1 ? "{count} item in your order" : "{count} items in your order", { count: units }) : t("Your cart is empty");
        document.getElementById("cartLineSummary").textContent = cart.size ? t(cart.size === 1 ? "{count} menu item selected" : "{count} menu items selected", { count: cart.size }) : t("Start with something lovely.");
        clearCart.disabled = cart.size === 0 || submitting;
        cartJson.value = JSON.stringify(Array.from(cart,([productId,quantity]) => ({ productId, quantity })));
        checkoutButton.disabled = !config.hasActiveShift || cart.size === 0 || submitting;
        cards.forEach(card => {
            const quantity = cart.get(Number(card.dataset.productId)) || 0;
            const badge = card.querySelector("[data-product-cart-count]");
            badge.hidden = quantity === 0;
            badge.textContent = t("{count} in cart", { count: quantity });
            card.classList.toggle("is-in-cart",quantity > 0);
        });
        window.auraPosI18n?.apply();
        if (focus) {
            const target = cartItems.querySelector(`[data-cart-product="${focus.id}"][data-cart-action="${focus.action}"]`)
                || cartItems.querySelector("[data-cart-action]") || (cartDialog.open ? document.getElementById("closeCart") : search);
            target.focus({ preventScroll: true });
        }
    }
    function filterProducts() {
        const term = search.value.trim().toLowerCase();
        let matches = 0;
        cards.forEach(card => {
            const visible = card.dataset.search.includes(term) && (category.value === "all" || card.dataset.categoryId === category.value);
            card.hidden = !visible;
            if (visible) matches++;
        });
        categoryButtons.forEach(button => {
            const selected = button.dataset.categoryFilter === category.value;
            button.classList.toggle("is-active",selected);
            button.setAttribute("aria-pressed",String(selected));
            if (selected) document.getElementById("catalogHeading").textContent = category.value === "all" ? t("All items") : button.dataset.categoryName;
        });
        document.getElementById("productResultCount").textContent = t(matches === 1 ? "{count} item available" : "{count} items available", { count: matches });
        document.getElementById("noProductMatches").hidden = matches > 0 || cards.length === 0;
        document.getElementById("clearSearch").hidden = !search.value;
    }
    document.querySelectorAll(".add-product").forEach(button => button.addEventListener("click",() => addProduct(Number(button.dataset.productId))));
    categoryButtons.forEach(button => button.addEventListener("click",() => { category.value = button.dataset.categoryFilter; filterProducts(); }));
    search.addEventListener("input",filterProducts);
    category.addEventListener("change",filterProducts);
    document.getElementById("clearSearch").addEventListener("click",() => { search.value = ""; filterProducts(); search.focus(); });
    document.getElementById("resetProductFilters").addEventListener("click",() => { search.value = ""; category.value = "all"; filterProducts(); search.focus(); });
    clearCart.addEventListener("click",async () => {
        if (submitting || cart.size === 0) return;
        if (!await window.auraDialog.confirm({ title: "Start a fresh order?", message: "All items will be removed from this cart. No saved order will be changed.", confirmText: "Clear cart", tone: "danger" })) return;
        cart.clear(); renderCart();
        (cartDialog.open ? document.getElementById("closeCart") : search).focus();
    });
    function updatePaymentHint() {
        const qrSelected = checkoutForm.querySelector('input[name="paymentChoice"]:checked')?.value === "qr";
        document.getElementById("qrNotice").hidden = !qrSelected;
        document.getElementById("cashPaymentHint").hidden = qrSelected;
    }
    checkoutForm.querySelectorAll('input[name="paymentChoice"]').forEach(input => input.addEventListener("change",updatePaymentHint));
    updatePaymentHint();
    checkoutForm.addEventListener("submit",event => {
        if (!config.hasActiveShift || cart.size === 0 || submitting) { event.preventDefault(); return; }
        submitting = true;
        checkoutForm.setAttribute("aria-busy","true");
        checkoutButton.disabled = true;
        checkoutButton.textContent = t("Creating order...");
        document.querySelectorAll(".add-product, #clearCart, [data-cart-action]").forEach(button => { button.disabled = true; });
        // Leave named form fields enabled so selected paymentChoice and antiforgery are submitted.
    });
    // Restore only a server-returned cart after a failed POST; no automatic cross-order/local-storage retry.
    try {
        const initial = JSON.parse(cartJson.value || "[]");
        if (Array.isArray(initial) && initial.length <= 50) initial.forEach(item => {
            if (products.has(item.productId) && Number.isInteger(item.quantity) && item.quantity >= 1 && item.quantity <= 100) cart.set(item.productId,item.quantity);
        });
    } catch { /* Malformed carts remain empty; server validation messages stay visible. */ }
    function translateProductActions() {
        document.querySelectorAll(".add-product").forEach(button => {
            const product = products.get(Number(button.dataset.productId));
            button.setAttribute("aria-label",t("Add {name} to order", { name: product.name }));
        });
    }
    window.addEventListener("aura:pos-language-changed",() => { renderCart(); filterProducts(); translateProductActions(); });
    renderCart(); filterProducts(); translateProductActions();
    window.addEventListener("pageshow",event => { if (event.persisted) window.location.reload(); });
})();

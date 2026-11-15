
(() => {
    "use strict";

    document.querySelectorAll("[data-pos-image]").forEach(img => {
        const fallback = () => { img.hidden = true; const icon = img.parentElement.querySelector(".pos-image-fallback"); if (icon) icon.hidden = false; };
        img.addEventListener("error", fallback);
        if (img.complete && img.naturalWidth === 0) fallback();
    });
    const config = window.auraPos;
    if (!config) return;

    const products = new Map(
        config.products.map(p => [p.id, p])
    );

    // Map<ProductId, Quantity>
    const cart = new Map();

    const cartItems = document.getElementById("cartItems");
    const cartTotal = document.getElementById("cartTotal");
    const cartJson = document.getElementById("cartJson");
    const checkoutButton =
        document.getElementById("checkoutButton");
    const checkoutForm =
        document.getElementById("checkoutForm");

    const searchInput =
        document.getElementById("productSearch");
    const categoryFilter =
        document.getElementById("categoryFilter");

    const money = cents =>
        "$" + (cents / 100).toFixed(2);

    function addProduct(productId) {
        if (!config.hasActiveShift) return;

        const product = products.get(productId);
        if (!product) return;

        const current = cart.get(productId) || 0;

        if (current >= 100) {
            alert("Maximum 100 units per product.");
            return;
        }

        if (!cart.has(productId) && cart.size >= 50) {
            alert("Maximum 50 different products per order.");
            return;
        }

        cart.set(productId, current + 1);
        renderCart();
    }

    function changeQuantity(productId, change) {
        const current = cart.get(productId);
        if (!current) return;

        const next = current + change;

        if (next <= 0) {
            cart.delete(productId);
        } else if (next <= 100) {
            cart.set(productId, next);
        }

        renderCart();
    }

    function removeProduct(productId) {
        cart.delete(productId);
        renderCart();
    }

    function renderCart() {
        cartItems.replaceChildren();

        let totalCents = 0;

        if (cart.size === 0) {
            const empty = document.createElement("p");
            empty.className = "text-muted text-center";
            empty.textContent = "Your cart is empty.";
            cartItems.appendChild(empty);
        }

        for (const [productId, quantity] of cart) {
            const product = products.get(productId);
            if (!product) continue;

            const subtotal = product.priceCents * quantity;
            totalCents += subtotal;

            const item = document.createElement("div");
            item.className = "pos-cart-item";

            const details = document.createElement("div");

            const name = document.createElement("strong");
            name.textContent = product.name;

            const price = document.createElement("div");
            price.className = "small text-muted";
            price.textContent =
                money(product.priceCents) + " each";

            details.append(name, price);

            const controls = document.createElement("div");
            controls.className = "pos-quantity-controls";

            const minus = document.createElement("button");
            minus.type = "button";
            minus.className = "btn btn-sm btn-outline-secondary";
            minus.textContent = "−";
            minus.setAttribute(
                "aria-label",
                "Decrease " + product.name
            );
            minus.addEventListener("click", () =>
                changeQuantity(productId, -1)
            );

            const count = document.createElement("span");
            count.textContent = String(quantity);

            const plus = document.createElement("button");
            plus.type = "button";
            plus.className = "btn btn-sm btn-outline-secondary";
            plus.textContent = "+";
            plus.setAttribute(
                "aria-label",
                "Increase " + product.name
            );
            plus.disabled = quantity >= 100;
            plus.addEventListener("click", () =>
                changeQuantity(productId, 1)
            );

            controls.append(minus, count, plus);

            const subtotalText =
                document.createElement("strong");
            subtotalText.textContent = money(subtotal);

            const remove = document.createElement("button");
            remove.type = "button";
            remove.className = "btn btn-sm btn-link text-danger";
            remove.textContent = "Remove";
            remove.addEventListener("click", () =>
                removeProduct(productId)
            );

            const right = document.createElement("div");
            right.className = "pos-cart-item-right";
            right.append(controls, subtotalText, remove);

            item.append(details, right);
            cartItems.appendChild(item);
        }

        cartTotal.textContent = money(totalCents);

        const items = Array.from(cart, ([productId, quantity]) => ({
            productId,
            quantity
        }));

        cartJson.value = JSON.stringify(items);

        checkoutButton.disabled =
            !config.hasActiveShift || items.length === 0;
    }

    function filterProducts() {
        const query = searchInput.value
            .trim()
            .toLowerCase();

        const category = categoryFilter.value;

        document.querySelectorAll(".pos-product-card")
            .forEach(card => {
                const matchesName =
                    card.dataset.name.includes(query);

                const matchesCategory =
                    category === "all" ||
                    card.dataset.categoryId === category;

                card.hidden = !(matchesName && matchesCategory);
            });
    }

    document.querySelectorAll(".add-product")
        .forEach(button => {
            button.addEventListener("click", () => {
                addProduct(Number(button.dataset.productId));
            });
        });

    document.getElementById("clearCart")
        .addEventListener("click", () => {
            cart.clear();
            renderCart();
        });

    searchInput.addEventListener("input", filterProducts);
    categoryFilter.addEventListener("change", filterProducts);

    checkoutForm.addEventListener("submit", event => {
        if (!config.hasActiveShift || cart.size === 0) {
            event.preventDefault();
            return;
        }

        if (!confirm("Create this order and continue to payment?")) {
            event.preventDefault();
            return;
        }

        checkoutButton.disabled = true;
        checkoutButton.textContent = "Creating order...";
    });

    renderCart();
})();

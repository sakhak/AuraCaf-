(() => {
    "use strict";
    const dialog = document.getElementById("auraActionDialog");
    if (!dialog) return;
    const title = document.getElementById("auraDialogTitle");
    const message = document.getElementById("auraDialogMessage");
    const confirmButton = document.getElementById("auraDialogConfirm");
    const cancelButton = document.getElementById("auraDialogCancel");
    let resolveDialog, previousFocus;
    function open(options, notice = false) {
        // Never approve an action when another dialog already owns the screen.
        if (resolveDialog) return Promise.resolve(false);
        const translate = text => window.auraPosI18n?.t(text) ?? text;
        title.textContent = translate(options.title || "One quick check");
        message.textContent = translate(options.message);
        confirmButton.textContent = translate(options.confirmText || (notice ? "Got it" : "Continue"));
        cancelButton.textContent = translate(options.cancelText || "Go back");
        document.getElementById("auraDialogDismiss").setAttribute("aria-label",translate("Close dialog"));
        cancelButton.hidden = notice;
        dialog.dataset.tone = options.tone || "primary";
        dialog.returnValue = "";
        previousFocus = document.activeElement;
        const result = new Promise(resolve => { resolveDialog = resolve; });
        dialog.showModal();
        (notice ? confirmButton : cancelButton).focus();
        return result;
    }
    confirmButton.addEventListener("click", () => dialog.close("confirmed"));
    cancelButton.addEventListener("click", () => dialog.close("cancelled"));
    document.getElementById("auraDialogDismiss").addEventListener("click", () => dialog.close("cancelled"));
    dialog.addEventListener("close", () => {
        const resolve = resolveDialog;
        resolveDialog = undefined;
        previousFocus?.focus();
        resolve?.(dialog.returnValue === "confirmed");
    });
    // Escape uses the native dialog cancel behavior, resolves false and restores focus.
    window.auraDialog = { confirm: options => open(options), notice: options => open(options, true) };

    function confirmationFor(form) {
        if (form.id === "checkoutForm") return { title: "Ready for checkout?", message: "We'll create this order and open payment. The server confirms the final total.", confirmText: "Create order" };
        if (form.id === "cashPaymentForm") return { title: "Cash received?", message: "Confirm only after you have physically received the customer's cash. The server will record the payment and calculate change.", confirmText: "Confirm cash payment" };
        if (form.hasAttribute("data-category-delete")) return { title: "Delete this category?", message: "Only an unused category can be deleted. Categories with products are protected.", confirmText: "Delete category", tone: "danger" };
        if (form.hasAttribute("data-confirm-deactivate")) return { title: "Deactivate this product?", message: "It will no longer be available for new orders. Historical orders will be retained.", confirmText: "Deactivate", tone: "danger" };
        const text = form.dataset.staffConfirm || form.dataset.inventoryConfirm || form.dataset.confirm || form.dataset.auraConfirm;
        if (!text) return null;
        const danger = /deactivat|remove|delete/i.test(text);
        return { title: danger ? "Confirm this change" : "One quick check", message: text, confirmText: danger ? "Confirm change" : "Continue", tone: danger ? "danger" : "primary" };
    }
    const approved = new WeakSet();
    const waiting = new WeakSet();
    document.addEventListener("submit", async event => {
        const form = event.target;
        if (!(form instanceof HTMLFormElement)) return;
        const options = confirmationFor(form);
        if (!options) return;
        if (approved.has(form)) { approved.delete(form); return; }
        event.preventDefault();
        event.stopImmediatePropagation();
        if (waiting.has(form)) return;
        waiting.add(form);
        const submitter = event.submitter;
        try {
            if (await window.auraDialog.confirm(options)) {
                if (!form.isConnected || (submitter && (!submitter.isConnected || submitter.disabled))) return;
                approved.add(form);
                try { form.requestSubmit(submitter || undefined); }
                finally { approved.delete(form); }
            }
        } finally { waiting.delete(form); }
    }, true);
})();

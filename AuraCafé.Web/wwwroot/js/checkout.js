(() => {
    "use strict";
    const cash = document.getElementById("cashCheckoutPanel");
    const qr = document.getElementById("qrCheckoutPanel");
    if (!cash || !qr) return;
    const show = document.getElementById("showCustomerQr");
    const dialog = document.getElementById("customerQrDialog");
    function showQr() {
        if (show && dialog && !dialog.open) dialog.showModal();
    }
    function selectMethod(openDisplay) {
        const isQr = document.querySelector('input[name="checkoutMethod"]:checked')?.value === "qr";
        cash.hidden = isQr;
        qr.hidden = !isQr;
        cash.querySelectorAll("input, button").forEach(control => { control.disabled = isQr; });
        if (isQr && openDisplay) showQr();
    }
    document.querySelectorAll('input[name="checkoutMethod"]').forEach(input => input.addEventListener("change", () => selectMethod(true)));
    show?.addEventListener("click", showQr);
    document.getElementById("closeCustomerQr")?.addEventListener("click", () => {
        if (dialog?.open) dialog.close();
    });
    dialog?.addEventListener("close", () => show?.focus());
    selectMethod(true);
})();

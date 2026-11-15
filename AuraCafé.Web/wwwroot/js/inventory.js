document.querySelectorAll("[data-inventory-confirm], [data-inventory-submit]").forEach(form => {
    let submitting = false;
    form.addEventListener("submit", event => {
        if (submitting) { event.preventDefault(); return; }
        submitting = true;
        form.setAttribute("aria-busy", "true");
        form.querySelectorAll("button[type=submit]").forEach(button => { button.disabled = true; button.textContent = "Processing..."; });
    });
});
window.addEventListener("pageshow", event => { if (event.persisted) window.location.reload(); });

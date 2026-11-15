document.querySelectorAll("[data-category-delete]").forEach(form => {
    form.addEventListener("submit", event => {
        if (!confirm("Delete this unused category? Categories with products cannot be deleted.")) event.preventDefault();
    });
});
document.querySelector("[data-category-save]")?.addEventListener("submit", event => {
    if (event.currentTarget.checkValidity()) {
        const button = event.currentTarget.querySelector("button[type=submit]");
        button.disabled = true;
        button.textContent = "Saving...";
        event.currentTarget.setAttribute("aria-busy", "true");
    }
});

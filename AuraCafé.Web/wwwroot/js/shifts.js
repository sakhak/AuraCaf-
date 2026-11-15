document.querySelectorAll("[data-shift-action]").forEach(form => {
    let submitting = false;
    form.addEventListener("submit", event => {
        if (submitting) { event.preventDefault(); return; }
        submitting = true;
        form.querySelectorAll("button[type=submit]").forEach(button => { button.disabled = true; button.textContent = "Processing..."; });
    });
});

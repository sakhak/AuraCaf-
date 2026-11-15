const position = document.querySelector("[data-staff-position]");
const passwordSection = document.querySelector("[data-staff-password]");
function showAccountFields() {
    if (!position || !passwordSection) return;
    const cashier = position.value === "Cashier";
    passwordSection.hidden = !cashier;
    const password = passwordSection.querySelector("input");
    password.disabled = !cashier;
    password.required = cashier;
    if (!cashier) password.value = "";
}
position?.addEventListener("change", showAccountFields);
showAccountFields();
document.querySelectorAll("[data-staff-save], [data-staff-confirm]").forEach(form => {
    let submitting = false;
    form.addEventListener("submit", event => {
        if (submitting) { event.preventDefault(); return; }
        submitting = true;
        form.setAttribute("aria-busy", "true");
        form.querySelectorAll("button[type=submit]").forEach(button => { button.disabled = true; button.textContent = "Saving..."; });
    });
});

document.getElementById("salesReportFilter")?.addEventListener("submit", event => {
    const button = event.currentTarget.querySelector("button[type=submit]");
    if (button) { button.disabled = true; button.textContent = "Loading report..."; }
    event.currentTarget.setAttribute("aria-busy", "true");
});

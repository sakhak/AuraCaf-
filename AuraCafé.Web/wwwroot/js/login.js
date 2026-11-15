(() => {
    "use strict";
    const input = document.getElementById("Input_Password");
    const button = document.getElementById("togglePassword");
    if (!input || !button) return;
    button.hidden = false;
    button.addEventListener("click", () => {
        const show = input.type === "password";
        input.type = show ? "text" : "password";
        button.textContent = show ? "Hide" : "Show";
        button.setAttribute("aria-pressed", String(show));
    });
})();

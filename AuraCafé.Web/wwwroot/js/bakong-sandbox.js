(() => {
    "use strict";
    const form = document.getElementById("sandboxCheck");
    if (!form) return;
    const status = document.getElementById("sandboxStatus");
    const button = form.querySelector("button");
    let timer, busy = false, stopped = false;
    async function check() {
        if (busy || stopped) return;
        clearTimeout(timer);
        if (document.hidden) { schedule(); return; }
        busy = true;
        button.disabled = true;
        try {
            const response = await fetch(form.action, { method: "POST", body: new FormData(form), headers: { "Accept": "application/json" } });
            if (response.redirected) { stopped = true; status.textContent = "Session changed. Refresh and sign in before continuing."; return; }
            const result = await response.json();
            if (!response.ok) { stopped = true; status.textContent = result.message || "Payment status is unknown. Use Check test payment to try again."; return; }
            if (result.status === "TestVerified") { stopped = true; location.reload(); return; }
            if (result.status === "Expired") { stopped = true; location.reload(); return; }
            status.textContent = "No confirmed test payment yet. Checking again in five seconds.";
        } catch { stopped = true; status.textContent = "Connection interrupted. Payment status is unknown. Use Check test payment to try again."; }
        finally { busy = false; button.disabled = false; schedule(); }
    }
    function schedule() { if (!stopped && form.dataset.autoCheck === "true") timer = setTimeout(check, 5000); }
    form.addEventListener("submit", event => { event.preventDefault(); stopped = false; check(); });
    window.addEventListener("pagehide", () => { stopped = true; clearTimeout(timer); });
    schedule();
})();

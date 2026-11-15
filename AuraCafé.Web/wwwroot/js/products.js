(() => {
    "use strict";
    const input = document.querySelector("[data-image-upload]");
    const preview = document.getElementById("productImagePreview");
    const status = document.getElementById("imagePreviewStatus");
    let objectUrl;
    function updatePreview() {
        if (objectUrl) { URL.revokeObjectURL(objectUrl); objectUrl = null; }
        preview.hidden = true;
        preview.removeAttribute("src");
        status.textContent = "";
        const file = input.files?.[0];
        if (file) {
            if (!["image/jpeg", "image/png", "image/webp"].includes(file.type) || file.size > 5 * 1024 * 1024) {
                status.textContent = "Choose JPG, PNG or WebP, no larger than 5 MB.";
                return;
            }
            objectUrl = URL.createObjectURL(file);
            preview.src = objectUrl;
        } else if (preview.dataset.currentImage) { preview.src = preview.dataset.currentImage; }
    }
    if (input && preview && status) {
        preview.addEventListener("load", () => { preview.hidden = false; status.textContent = "Image preview loaded."; });
        preview.addEventListener("error", () => { preview.hidden = true; status.textContent = "Image could not be displayed. Choose another image."; });
        input.addEventListener("change", updatePreview);
        updatePreview();
    }
    window.addEventListener("pagehide", () => { if (objectUrl) URL.revokeObjectURL(objectUrl); });
    document.querySelectorAll("[data-product-image]").forEach(img => {
        img.addEventListener("error", () => { img.hidden = true; });
        if (img.complete && img.naturalWidth === 0) img.hidden = true;
    });
    document.querySelectorAll("[data-confirm-deactivate]").forEach(form => {
        form.addEventListener("submit", event => {
            if (!confirm("Deactivate this product? It will no longer be available for new orders. Historical orders will be retained.")) event.preventDefault();
        });
    });
})();

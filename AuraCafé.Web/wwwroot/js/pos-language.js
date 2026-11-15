(() => {
    "use strict";
    const selector = document.getElementById("posLanguage");
    if (!selector) return;
    const khmer = {
        "Language": "ភាសា",
        "Point of Sale": "ចំណុចលក់",
        "YOUR COUNTER, MADE SIMPLE": "ការលក់កាន់តែងាយស្រួល",
        "Pick a favorite. Build the order. Make their day.": "ជ្រើសរើសមុខទំនិញ រៀបចំការបញ្ជាទិញ និងបម្រើអតិថិជន។",
        "Cashier corner": "ផ្នែកអ្នកគិតលុយ",
        "Cashier corner ♡": "ផ្នែកអ្នកគិតលុយ ♡",
        "A little care in every cup": "យកចិត្តទុកដាក់ក្នុងគ្រប់ពែង",
        "Dashboard": "ផ្ទាំងគ្រប់គ្រង",
        "My Orders": "ការបញ្ជាទិញរបស់ខ្ញុំ",
        "My Shift": "វេនរបស់ខ្ញុំ",
        "Logout": "ចាកចេញ",
        "Shift open": "កំពុងបើកវេន",
        "Clock in required": "ត្រូវចូលវេនជាមុន",
        "Status unavailable": "មិនអាចបង្ហាញស្ថានភាពបាន",
        "Choose items": "ជ្រើសរើសមុខទំនិញ",
        "Review cart": "ពិនិត្យកន្ត្រក",
        "Collect payment": "ទទួលការទូទាត់",
        "Checkout steps": "ជំហាននៃការទូទាត់",
        "Let's open your shift first.": "សូមចូលវេនរបស់អ្នកជាមុនសិន។",
        "Count your opening drawer and clock in before creating an order.": "រាប់សាច់ប្រាក់ដើមវេន រួចចូលវេន មុនបង្កើតការបញ្ជាទិញ។",
        "Open My Shift": "បើកវេនរបស់ខ្ញុំ",
        "Refresh POS": "ផ្ទុកទំព័រលក់ឡើងវិញ",
        "THE MENU": "ម៉ឺនុយ",
        "Categories": "ប្រភេទទំនិញ",
        "All items": "មុខទំនិញទាំងអស់",
        "Product categories": "ប្រភេទផលិតផល",
        "Filter products by category": "ច្រោះផលិតផលតាមប្រភេទ",
        "Made to order,": "រៀបចំតាមការបញ្ជាទិញ",
        "served with a little care.": "បម្រើដោយការយកចិត្តទុកដាក់។",
        "Search menu by product or description": "ស្វែងរកតាមឈ្មោះ ឬការពិពណ៌នាផលិតផល",
        "Search coffee, tea, or a favorite...": "ស្វែងរកកាហ្វេ តែ ឬមុខទំនិញ...",
        "Clear product search": "សម្អាតពាក្យស្វែងរក",
        "Add": "បន្ថែម",
        "No matching favorites": "រកមិនឃើញមុខទំនិញដែលត្រូវគ្នា",
        "Try another search or browse all items. Your cart will stay here.": "សាកល្បងស្វែងរកម្តងទៀត ឬមើលមុខទំនិញទាំងអស់។ កន្ត្រករបស់អ្នកមិនបាត់ទេ។",
        "Show all items": "បង្ហាញមុខទំនិញទាំងអស់",
        "The menu is empty": "មិនទាន់មានមុខទំនិញក្នុងម៉ឺនុយ",
        "No available products found. Ask an Admin to check product availability.": "មិនមានផលិតផលសម្រាប់លក់ទេ។ សូមឱ្យអ្នកគ្រប់គ្រងពិនិត្យផលិតផល។",
        "REVIEW BEFORE CHECKOUT": "ពិនិត្យមុនពេលទូទាត់",
        "Current order": "ការបញ្ជាទិញបច្ចុប្បន្ន",
        "Close cart": "បិទកន្ត្រក",
        "Start with something lovely.": "ចាប់ផ្តើមដោយជ្រើសរើសមុខទំនិញ។",
        "Clear cart": "សម្អាតកន្ត្រក",
        "Current order items": "មុខទំនិញក្នុងការបញ្ជាទិញបច្ចុប្បន្ន",
        "A little empty here": "កន្ត្រកនៅទទេ",
        "Tap Add on a menu item to start your order.": "ចុច បន្ថែម លើមុខទំនិញ ដើម្បីចាប់ផ្តើមការបញ្ជាទិញ។",
        "Order estimate": "តម្លៃប៉ាន់ស្មាន",
        "USD · final total confirmed at checkout": "USD · បញ្ជាក់តម្លៃសរុបពេលទូទាត់",
        "How will they pay?": "អតិថិជនទូទាត់តាមរបៀបណា?",
        "Cash": "សាច់ប្រាក់",
        "USD payment": "ទូទាត់ជា USD",
        "Saved QR": "កូដ QR ដែលបានរក្សាទុក",
        "Display only": "សម្រាប់បង្ហាញប៉ុណ្ណោះ",
        "Payment stays pending until verified. Saved QR display does not confirm payment.": "ការទូទាត់នៅរង់ចាំរហូតដល់បានផ្ទៀងផ្ទាត់។ ការបង្ហាញកូដ QR មិនមែនជាការបញ្ជាក់ថាបានទទួលប្រាក់ទេ។",
        "Enter cash received on the next screen.": "បញ្ចូលសាច់ប្រាក់ដែលបានទទួលនៅទំព័របន្ទាប់។",
        "Continue to payment": "បន្តទៅការទូទាត់",
        "Optional receipt after a confirmed cash payment.": "អាចបោះពុម្ពបង្កាន់ដៃ បន្ទាប់ពីបញ្ជាក់ការទូទាត់សាច់ប្រាក់។",
        "Your cart is empty": "កន្ត្រករបស់អ្នកនៅទទេ",
        "View cart": "មើលកន្ត្រក",
        "{count} item available": "មាន {count} មុខទំនិញ",
        "{count} items available": "មាន {count} មុខទំនិញ",
        "{count} item in your order": "មានទំនិញ {count} ក្នុងការបញ្ជាទិញ",
        "{count} items in your order": "មានទំនិញ {count} ក្នុងការបញ្ជាទិញ",
        "{count} menu item selected": "បានជ្រើសរើស {count} មុខទំនិញ",
        "{count} menu items selected": "បានជ្រើសរើស {count} មុខទំនិញ",
        "{count} in cart": "{count} ក្នុងកន្ត្រក",
        "{price} each": "{price} ក្នុងមួយឯកតា",
        "Add {name} to order": "បន្ថែម {name} ទៅការបញ្ជាទិញ",
        "Decrease {name}": "បន្ថយចំនួន {name}",
        "Increase {name}": "បង្កើនចំនួន {name}",
        "Remove {name}": "ដក {name} ចេញ",
        "Remove": "ដកចេញ",
        "{name} added. {count} in your order.": "បានបន្ថែម {name}។ មានចំនួន {count} ក្នុងការបញ្ជាទិញ។",
        "{name} quantity: {count}": "ចំនួន {name}៖ {count}",
        "{name} removed from the order.": "បានដក {name} ចេញពីការបញ្ជាទិញ។",
        "Creating order...": "កំពុងបង្កើតការបញ្ជាទិញ...",
        "That's a full batch!": "ដល់ចំនួនអតិបរមាហើយ!",
        "You can add up to 100 units of each product.": "អ្នកអាចបន្ថែមផលិតផលនីមួយៗបានរហូតដល់ 100 ឯកតា។",
        "Your tray is full": "កន្ត្រកដល់ចំនួនកំណត់ហើយ",
        "An order can contain up to 50 different products.": "ការបញ្ជាទិញមួយអាចមានផលិតផលខុសគ្នារហូតដល់ 50 មុខ។",
        "Start a fresh order?": "ចាប់ផ្តើមការបញ្ជាទិញថ្មីមែនទេ?",
        "All items will be removed from this cart. No saved order will be changed.": "មុខទំនិញទាំងអស់នឹងត្រូវដកចេញពីកន្ត្រកនេះ។ ការបញ្ជាទិញដែលបានរក្សាទុកមិនត្រូវបានកែប្រែទេ។",
        "Ready for checkout?": "រួចរាល់សម្រាប់ទូទាត់ហើយឬនៅ?",
        "We'll create this order and open payment. The server confirms the final total.": "ប្រព័ន្ធនឹងបង្កើតការបញ្ជាទិញនេះ ហើយបើកទំព័រទូទាត់ ដោយបញ្ជាក់តម្លៃសរុបចុងក្រោយ។",
        "Create order": "បង្កើតការបញ្ជាទិញ",
        "One quick check": "សូមពិនិត្យបន្តិច",
        "Go back": "ត្រឡប់ក្រោយ",
        "Continue": "បន្ត",
        "Got it": "យល់ហើយ",
        "Close dialog": "បិទផ្ទាំងបញ្ជាក់"
    };
    const reverse = new Map(Object.entries(khmer).map(([english, translation]) => [translation, english]));
    let language = "en";
    try { if (localStorage.getItem("aura.posLanguage") === "km") language = "km"; } catch { }
    function t(english, values = {}) {
        let result = language === "km" ? (khmer[english] || english) : english;
        for (const [name, value] of Object.entries(values)) result = result.replaceAll("{" + name + "}", () => String(value));
        return result;
    }
    function translateValue(value) {
        const key = value.trim();
        const english = Object.hasOwn(khmer, key) ? key : reverse.get(key);
        return english ? value.replace(key, t(english)) : value;
    }
    function apply() {
        const roots = document.querySelectorAll(".pos-workspace, .portal-sidebar, .portal-header, #auraActionDialog");
        roots.forEach(root => {
            const walker = document.createTreeWalker(root, NodeFilter.SHOW_TEXT);
            const nodes = [];
            while (walker.nextNode()) nodes.push(walker.currentNode);
            nodes.forEach(node => {
                if (node.parentElement?.closest("[data-pos-no-translate], script, style, textarea, noscript")) return;
                node.nodeValue = translateValue(node.nodeValue);
            });
            root.querySelectorAll("[placeholder], [aria-label]").forEach(element => {
                if (element.closest("[data-pos-no-translate]")) return;
                for (const attribute of ["placeholder", "aria-label"]) {
                    if (element.hasAttribute(attribute)) element.setAttribute(attribute, translateValue(element.getAttribute(attribute)));
                }
            });
        });
    }
    function setLanguage(value) {
        language = value === "km" ? "km" : "en";
        selector.value = language;
        document.documentElement.lang = language;
        document.title = t("Point of Sale") + " - Aura Café";
        document.body.classList.toggle("pos-khmer", language === "km");
        try { localStorage.setItem("aura.posLanguage", language); } catch { }
        apply();
        window.dispatchEvent(new CustomEvent("aura:pos-language-changed"));
    }
    window.auraPosI18n = { t, apply };
    selector.addEventListener("change", () => setLanguage(selector.value));
    setLanguage(language);
})();

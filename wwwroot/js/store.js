(() => {
    const cartKey = "tech-haven-cart";
    const grid = document.querySelector("#product-grid");
    const categoryFilter = document.querySelector("#category-filter");
    const itemsElement = document.querySelector("#cart-items");
    const countElement = document.querySelector("#cart-count");
    const totalElement = document.querySelector("#cart-total");
    const payloadElement = document.querySelector("#cart-payload");
    const checkoutButton = document.querySelector("#checkout-button");

    let cart = readCart();

    function readCart() {
        try {
            const saved = JSON.parse(localStorage.getItem(cartKey) || "[]");
            if (!Array.isArray(saved)) return [];
            return saved.filter(item =>
                Number.isSafeInteger(item.id) && item.id > 0 &&
                Number.isSafeInteger(item.quantity) && item.quantity > 0);
        } catch {
            return [];
        }
    }

    function formatPrice(value) {
        return new Intl.NumberFormat("vi-VN").format(value) + " ₫";
    }

    function saveCart() {
        localStorage.setItem(cartKey, JSON.stringify(cart));
        renderCart();
    }

    function renderCart() {
        itemsElement.replaceChildren();
        let itemCount = 0;
        let total = 0;

        if (cart.length === 0) {
            const empty = document.createElement("div");
            empty.className = "cart-empty";
            empty.textContent = "Chọn một vài món công nghệ để bắt đầu.";
            itemsElement.append(empty);
        }

        cart.forEach((item, index) => {
            itemCount += item.quantity;
            total += item.price * item.quantity;

            const row = document.createElement("div");
            row.className = "cart-row";

            const name = document.createElement("span");
            name.className = "cart-product";
            name.textContent = item.name;

            const lineTotal = document.createElement("strong");
            lineTotal.className = "cart-line-total";
            lineTotal.textContent = formatPrice(item.price * item.quantity);

            const controls = document.createElement("div");
            controls.className = "cart-row-controls";

            const decrement = document.createElement("button");
            decrement.type = "button";
            decrement.className = "cart-quantity-button";
            decrement.setAttribute("aria-label", `Giảm số lượng ${item.name}`);
            decrement.textContent = "−";
            decrement.addEventListener("click", () => updateQuantity(index, -1));

            const quantity = document.createElement("span");
            quantity.className = "cart-quantity";
            quantity.textContent = String(item.quantity);

            const increment = document.createElement("button");
            increment.type = "button";
            increment.className = "cart-quantity-button";
            increment.setAttribute("aria-label", `Tăng số lượng ${item.name}`);
            increment.textContent = "+";
            increment.addEventListener("click", () => updateQuantity(index, 1));

            const remove = document.createElement("button");
            remove.type = "button";
            remove.className = "cart-remove";
            remove.textContent = "Xóa";
            remove.addEventListener("click", () => {
                cart.splice(index, 1);
                saveCart();
            });

            controls.append(decrement, quantity, increment, remove);
            row.append(name, lineTotal, controls);
            itemsElement.append(row);
        });

        countElement.textContent = String(itemCount);
        totalElement.textContent = formatPrice(total);
        checkoutButton.disabled = cart.length === 0;
        payloadElement.replaceChildren();

        cart.forEach((item, index) => {
            for (const [name, value] of [
                [`Items[${index}].ProductId`, item.id],
                [`Items[${index}].Quantity`, item.quantity]
            ]) {
                const input = document.createElement("input");
                input.type = "hidden";
                input.name = name;
                input.value = String(value);
                payloadElement.append(input);
            }
        });
    }

    function updateQuantity(index, change) {
        cart[index].quantity += change;
        if (cart[index].quantity <= 0) cart.splice(index, 1);
        saveCart();
    }

    grid.addEventListener("click", event => {
        const button = event.target.closest(".add-to-cart");
        if (!button) return;

        const id = Number(button.dataset.productId);
        const product = {
            id,
            name: button.dataset.productName,
            price: Number(button.dataset.productPrice)
        };
        const existing = cart.find(item => item.id === id);
        if (existing) existing.quantity++;
        else cart.push({ ...product, quantity: 1 });
        saveCart();
    });

    categoryFilter.addEventListener("change", async () => {
        categoryFilter.disabled = true;
        try {
            const response = await fetch(`/Home/Filter?category=${encodeURIComponent(categoryFilter.value)}`);
            if (!response.ok) throw new Error("Không thể tải sản phẩm theo danh mục.");
            grid.innerHTML = await response.text();
        } catch {
            const error = document.createElement("div");
            error.className = "catalog-empty";
            error.textContent = "Chưa thể tải danh mục. Vui lòng thử lại.";
            grid.replaceChildren(error);
        } finally {
            categoryFilter.disabled = false;
        }
    });

    renderCart();
})();

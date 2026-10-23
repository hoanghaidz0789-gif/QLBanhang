(() => {
    const cartKey = "tech-haven-cart";
    const products = new Map(
        Array.from(document.querySelectorAll(".checkout-products [data-product-id]"), element => [
            Number(element.dataset.productId),
            { name: element.dataset.productName, price: Number(element.dataset.productPrice) }
        ]));
    const cartElement = document.querySelector("#checkout-items");
    const hiddenItems = document.querySelector("#checkout-hidden-items");
    const totalElement = document.querySelector("#checkout-total");
    const submitButton = document.querySelector("#place-order-button");

    let cart;
    try {
        cart = JSON.parse(localStorage.getItem(cartKey) || "[]");
    } catch {
        cart = [];
    }

    if (!Array.isArray(cart)) cart = [];
    cart = cart.filter(item =>
        Number.isSafeInteger(item.id) && item.id > 0 &&
        Number.isSafeInteger(item.quantity) && item.quantity > 0 &&
        products.has(item.id));

    const total = cart.reduce((sum, item) => sum + products.get(item.id).price * item.quantity, 0);
    totalElement.textContent = new Intl.NumberFormat("vi-VN").format(total) + " ₫";

    if (cart.length === 0) {
        const empty = document.createElement("p");
        empty.className = "checkout-empty";
        empty.textContent = "Giỏ hàng đang trống. Vui lòng chọn sản phẩm trước khi đặt hàng.";
        cartElement.append(empty);
        submitButton.disabled = true;
    }

    cart.forEach((item, index) => {
        const product = products.get(item.id);
        const row = document.createElement("div");
        row.className = "checkout-line";
        const name = document.createElement("span");
        name.textContent = `${product.name} × ${item.quantity}`;
        const price = document.createElement("strong");
        price.textContent = new Intl.NumberFormat("vi-VN").format(product.price * item.quantity) + " ₫";
        row.append(name, price);
        cartElement.append(row);

        for (const [key, value] of [
            [`Items[${index}].ProductId`, item.id],
            [`Items[${index}].Quantity`, item.quantity]
        ]) {
            const input = document.createElement("input");
            input.type = "hidden";
            input.name = key;
            input.value = String(value);
            hiddenItems.append(input);
        }
    });
})();

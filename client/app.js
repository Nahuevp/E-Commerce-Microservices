const API_BASE = ''; // Use relative URLs (works both locally and behind nginx)
const tokenKey = 'ecommerce_jwt';

// Health check timeout (ms)
const HEALTH_CHECK_TIMEOUT = 5000;
const SERVICE_REFRESH_INTERVAL = 30000;
const TOAST_AUTO_HIDE_DELAY = 5000;

// Service endpoints for health checks (relative URLs - works with nginx)
const SERVICES_CONFIG = [
    { name: 'product-service', url: '/api/products/health', icon: '📦' },
    { name: 'order-service', url: '/api/orders/health', icon: '📋' },
    { name: 'cart-service', url: '/api/carts/health', icon: '🛒' },
    { name: 'payment-service', url: '/api/payments/health', icon: '💳' },
    { name: 'notification-service', url: '/api/notifications/health', icon: '🔔' },
    { name: 'inventory-service', url: '/api/inventory/health', icon: '📊' }
];

// Cart state
let currentCart = null;
let currentCartId = null;
let serviceHealthStatus = {};

// DOM Elements
const authSection = document.getElementById('auth-section');
const dashboardSection = document.getElementById('dashboard-section');
const ordersSection = document.getElementById('orders-section');
const authForm = document.getElementById('auth-form');
const authMessage = document.getElementById('auth-message');
const mainNav = document.getElementById('main-nav');
const productsList = document.getElementById('products-list');
const ordersList = document.getElementById('orders-list');
const addProductFormContainer = document.getElementById('add-product-form-container');
const addProductForm = document.getElementById('add-product-form');
const btnToggleAddProduct = document.getElementById('btn-toggle-add-product');
const btnCancelEdit = document.getElementById('btn-cancel-edit');
const formTitle = document.getElementById('form-title');
const btnSaveProduct = document.getElementById('btn-save-product');

// Auth form elements
const toggleLogin = document.getElementById('toggle-login');
const toggleRegister = document.getElementById('toggle-register');
const authTitle = document.getElementById('auth-title');
const authSubtitle = document.getElementById('auth-subtitle');
const btnAuthSubmit = document.getElementById('btn-auth-submit');

// Navigation elements
const navProducts = document.getElementById('nav-products');
const navOrders = document.getElementById('nav-orders');
const btnOpenCart = document.getElementById('btn-open-cart');
const btnLogout = document.getElementById('btn-logout');
const userInfo = document.getElementById('user-info');
const userName = document.getElementById('user-name');

// Modal elements
const cartModal = document.getElementById('cart-modal');
const checkoutModal = document.getElementById('checkout-modal');
const cartBadge = document.getElementById('cart-badge');
const servicesList = document.getElementById('services-list');
const serviceStatusPanel = document.getElementById('service-status');

// Toast container
const toastContainer = document.getElementById('toast-container');

// Initialization
document.addEventListener('DOMContentLoaded', () => {
    checkAuth();
    initEventListeners();
    initCartEventListeners();
    initCheckoutEventListeners();
    initServiceStatusEventListeners();
});

function initEventListeners() {
    // Auth - Toggle between Login and Register
    let authMode = 'login';
    
    if (toggleLogin) {
        toggleLogin.addEventListener('click', () => {
            authMode = 'login';
            toggleLogin.classList.add('active');
            toggleRegister.classList.remove('active');
            authTitle.textContent = 'Welcome Back';
            authSubtitle.textContent = 'Sign in to your account';
            btnAuthSubmit.textContent = 'Sign In';
        });
    }
    
    if (toggleRegister) {
        toggleRegister.addEventListener('click', () => {
            authMode = 'register';
            toggleRegister.classList.add('active');
            toggleLogin.classList.remove('active');
            authTitle.textContent = 'Create Account';
            authSubtitle.textContent = 'Register for a new account';
            btnAuthSubmit.textContent = 'Register';
        });
    }
    
    // Auth form submission
    if (authForm) {
        authForm.addEventListener('submit', (e) => {
            e.preventDefault();
            handleAuth(e, authMode);
        });
    }

    // Navigation Tabs
    if (navProducts) {
        navProducts.addEventListener('click', () => {
            showView('products');
        });
    }
    
    if (navOrders) {
        navOrders.addEventListener('click', () => {
            showView('orders');
        });
    }
    
    // Cart button
    if (btnOpenCart) {
        btnOpenCart.addEventListener('click', openCartModal);
    }
    
    // Logout button
    if (btnLogout) {
        btnLogout.addEventListener('click', logout);
    }

    // Products
    if (btnToggleAddProduct) {
        btnToggleAddProduct.addEventListener('click', () => {
            resetProductForm();
            addProductFormContainer.classList.toggle('hidden');
        });
    }

    if (btnCancelEdit) {
        btnCancelEdit.addEventListener('click', () => {
            resetProductForm();
            addProductFormContainer.classList.add('hidden');
        });
    }

    if (addProductForm) {
        addProductForm.addEventListener('submit', handleAddProduct);
    }

    // Continue shopping button in cart
    document.getElementById('btn-continue-shopping')?.addEventListener('click', closeCartModal);

    // Card number formatting
    document.getElementById('card-number')?.addEventListener('input', (e) => {
        let value = e.target.value.replace(/\s+/g, '').replace(/[^0-9]/gi, '');
        let formatted = value.match(/.{1,4}/g)?.join(' ') || value;
        e.target.value = formatted;
    });

    // Card expiry formatting
    document.getElementById('card-expiry')?.addEventListener('input', (e) => {
        let value = e.target.value.replace(/\s+/g, '').replace(/[^0-9]/gi, '');
        if (value.length >= 2) {
            value = value.substring(0, 2) + '/' + value.substring(2, 4);
        }
        e.target.value = value;
    });
}

function initCartEventListeners() {
    // Cart button (in header)
    document.getElementById('btn-open-cart')?.addEventListener('click', openCartModal);
    
    // Close buttons - including the new cart close button
    document.querySelector('.close-cart')?.addEventListener('click', closeCartModal);
    document.querySelector('.cart-close-btn')?.addEventListener('click', closeCartModal);
    
    // Close on overlay click
    cartModal?.addEventListener('click', (e) => {
        if (e.target === cartModal) closeCartModal();
    });

    // Checkout button
    document.getElementById('btn-checkout')?.addEventListener('click', openCheckoutWizard);
}

function initCheckoutEventListeners() {
    // Close buttons
    document.querySelector('.close-checkout')?.addEventListener('click', closeCheckoutModal);
    
    // Close on overlay click
    checkoutModal?.addEventListener('click', (e) => {
        if (e.target === checkoutModal) closeCheckoutModal();
    });

    // Wizard navigation
    document.getElementById('btn-step1-next')?.addEventListener('click', () => {
        // Update payment amount in step 2 before navigating
        const paymentAmountEl = document.getElementById('payment-amount');
        if (paymentAmountEl) {
            paymentAmountEl.textContent = `$${checkoutData.total.toFixed(2)}`;
        }
        goToStep(2);
    });
    document.getElementById('btn-step2-prev')?.addEventListener('click', () => goToStep(1));
    document.getElementById('btn-submit-payment')?.addEventListener('click', submitPayment);
    document.getElementById('btn-finish-checkout')?.addEventListener('click', () => {
        closeCheckoutModal();
        loadOrders();
        showDashboard();
    });
}

function initServiceStatusEventListeners() {
    document.getElementById('btn-refresh-status')?.addEventListener('click', checkServicesHealth);
}

// Authentication
function checkAuth() {
    const token = localStorage.getItem(tokenKey);
    if (token) {
        const email = getEmailFromToken(token) || 'user';
        showDashboard(email);
    } else {
        showAuth();
    }
}

function getEmailFromToken(token) {
    try {
        const payload = JSON.parse(atob(token.split('.')[1]));
        return payload.email;
    } catch (e) {
        return null;
    }
}

function getUserId() {
    const token = localStorage.getItem(tokenKey);
    if (!token) return null;
    try {
        const payload = JSON.parse(atob(token.split('.')[1]));
        return payload.id || payload.userId || null;
    } catch (e) {
        return null; // Don't fallback to 1 - require login
    }
}

// UI State Management
function showAuth() {
    authSection.classList.remove('hidden');
    dashboardSection.classList.add('hidden');
    ordersSection.classList.add('hidden');
    mainNav?.classList.add('hidden');
    userInfo?.classList.add('hidden');
    btnLogout?.classList.add('hidden');
    btnOpenCart?.classList.add('hidden');
    serviceStatusPanel.classList.add('hidden');
}

function showDashboard(email) {
    authSection.classList.add('hidden');
    dashboardSection.classList.remove('hidden');
    ordersSection.classList.add('hidden');
    
    // Show navigation and user info
    mainNav?.classList.remove('hidden');
    btnLogout?.classList.remove('hidden');
    userInfo?.classList.remove('hidden');
    btnOpenCart?.classList.remove('hidden');
    
    // Set user name from email (or from token)
    if (userName && email) {
        userName.textContent = email.split('@')[0]; // Show just the name part
    }
    
    // Reset to products view
    showView('products');
    
    serviceStatusPanel.classList.remove('hidden');
    loadProducts();
    loadCart();
    checkServicesHealth();
    startServiceStatusRefresh();
}

function showView(view) {
    if (view === 'products') {
        dashboardSection.classList.remove('hidden');
        ordersSection.classList.add('hidden');
        navProducts?.classList.add('active');
        navOrders?.classList.remove('active');
        loadProducts();
    } else if (view === 'orders') {
        dashboardSection.classList.add('hidden');
        ordersSection.classList.remove('hidden');
        navProducts?.classList.remove('active');
        navOrders?.classList.add('active');
        loadOrders();
    }
}

function logout() {
    localStorage.removeItem(tokenKey);
    stopServiceStatusRefresh();
    checkAuth();
}

// Authentication API
async function handleAuth(e, action) {
    e.preventDefault();
    const email = document.getElementById('email').value;
    const password = document.getElementById('password').value;

    if (!email || !password) {
        showMessage('Please enter both email and password', 'error');
        return;
    }

    try {
        const response = await fetch(`${API_BASE}/api/auth/${action}`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ email, passwordHash: password })
        });

        if (response.ok) {
            if (action === 'login') {
                const data = await response.json();
                localStorage.setItem(tokenKey, data.token);
                showDashboard(email);
            } else {
                showMessage('Registered successfully. You can now login.', 'success');
            }
        } else {
            const err = await response.text();
            showMessage(err || 'Authentication failed', 'error');
        }
    } catch (error) {
        showMessage('Connection to API Gateway failed.', 'error');
        console.error(error);
    }
}

// Fetch Data with Authorization
async function fetchWithAuth(url, options = {}) {
    const token = localStorage.getItem(tokenKey);
    if (!token) return { ok: false, status: 401 };

    const headers = {
        'Content-Type': 'application/json',
        'Authorization': `Bearer ${token}`,
        ...options.headers
    };

    const fetchOptions = {
        ...options,
        headers
    };

    // Use AbortController for timeout
    if (options.timeout !== 0) {
        const controller = new AbortController();
        const timeoutId = setTimeout(() => controller.abort(), options.timeout || 10000);
        fetchOptions.signal = controller.signal;
        
        try {
            const response = await fetch(url, fetchOptions);
            clearTimeout(timeoutId);
            return response;
        } catch (error) {
            clearTimeout(timeoutId);
            if (error.name === 'AbortError') {
                return { ok: false, status: 0, timeout: true };
            }
            throw error;
        }
    }

    return fetch(url, fetchOptions);
}

// Products
async function loadProducts() {
    productsList.innerHTML = '<p class="loading">Loading products...</p>';
    try {
        const response = await fetchWithAuth(`${API_BASE}/api/products`);
        if (response.ok) {
            const products = await response.json();
            renderProducts(products);
        } else if (response.status === 401) {
            logout();
        } else {
            productsList.innerHTML = '<p class="error">Failed to load products.</p>';
        }
    } catch (error) {
        productsList.innerHTML = '<p class="error">Connection error.</p>';
        console.error(error);
    }
}

function renderProducts(products) {
    // Filter out invalid products (id <= 0)
    const validProducts = products.filter(p => p.id && p.id > 0);
    
    if (validProducts.length === 0) {
        productsList.innerHTML = '<p>No products available. Add some directly to the DB!</p>';
        return;
    }

    productsList.innerHTML = validProducts.map(p => {
        // Determine stock status
        const stockStatus = p.stock === 0 ? 'out-of-stock' : p.stock <= 5 ? 'low-stock' : 'in-stock';
        const stockText = p.stock === 0 ? 'Out of Stock' : p.stock <= 5 ? `Only ${p.stock} left` : `${p.stock} in stock`;
        
        return `
            <div class="product-card">
                <div class="product-image">
                    <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="1.5">
                        <path stroke-linecap="round" stroke-linejoin="round" d="M20.25 7.5l-.625 10.632a2.25 2.25 0 01-2.247 2.118H6.622a2.25 2.25 0 01-2.247-2.118L3.75 7.5M10 11.25h4M3.375 7.5h17.25c.621 0 1.125-.504 1.125-1.125v-1.5c0-.621-.504-1.125-1.125-1.125H3.375c-.621 0-1.125.504-1.125 1.125v1.5c0 .621.504 1.125 1.125 1.125z" />
                    </svg>
                </div>
                <div class="product-content">
                    <h3 class="product-name">${escapeHtml(p.name)}</h3>
                    <p class="product-price">$${p.price.toFixed(2)}</p>
                    <p class="product-stock ${stockStatus}">
                        <span class="stock-dot"></span>
                        ${stockText}
                    </p>
                    <div class="product-actions">
                        <button onclick="addToCart(${p.id}, ${p.price})" class="btn-add-cart" ${p.stock === 0 ? 'disabled' : ''}>
                            <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
                                <path stroke-linecap="round" stroke-linejoin="round" d="M12 4.5v15m7.5-7.5h-15" />
                            </svg>
                            Add to Cart
                        </button>
                        <button onclick="editProduct(${p.id}, '${escapeHtml(p.name).replace(/'/g, "\\'")}', ${p.price}, ${p.stock})" class="btn secondary small">✏️</button>
                        <button onclick="deleteProduct(${p.id})" class="btn secondary small btn-danger">🗑️</button>
                    </div>
                </div>
            </div>
        `;
    }).join('');
}

function escapeHtml(text) {
    const div = document.createElement('div');
    div.textContent = text;
    return div.innerHTML;
}

async function handleAddProduct(e) {
    e.preventDefault();
    const id = document.getElementById('prod-id').value;
    const name = document.getElementById('prod-name').value;
    const price = parseFloat(document.getElementById('prod-price').value);
    const stock = parseInt(document.getElementById('prod-stock').value);

    const isEditing = id !== "";
    const url = isEditing ? `${API_BASE}/api/products/${id}` : `${API_BASE}/api/products`;
    const method = isEditing ? 'PUT' : 'POST';

    try {
        const response = await fetchWithAuth(url, {
            method: method,
            body: JSON.stringify({ name, price, stock })
        });

        if (response.ok) {
            showToast(`Product ${isEditing ? 'updated' : 'added'} successfully!`, 'success');
            resetProductForm();
            addProductFormContainer.classList.add('hidden');
            loadProducts();
        } else if (response.status === 401) {
            logout();
        } else {
            showToast(`Failed to ${isEditing ? 'update' : 'add'} product`, 'error');
        }
    } catch (error) {
        showToast('Connection error', 'error', () => handleAddProduct(e));
        console.error(error);
    }
}

function editProduct(id, name, price, stock) {
    document.getElementById('prod-id').value = id;
    document.getElementById('prod-name').value = name;
    document.getElementById('prod-price').value = price;
    document.getElementById('prod-stock').value = stock;

    formTitle.textContent = 'Edit Product';
    btnSaveProduct.textContent = 'Update Product';
    btnCancelEdit.classList.remove('hidden');
    addProductFormContainer.classList.remove('hidden');
    
    addProductFormContainer.scrollIntoView({ behavior: 'smooth' });
}

async function deleteProduct(id) {
    if (!confirm('Are you sure you want to delete this product?')) return;

    try {
        const response = await fetchWithAuth(`${API_BASE}/api/products/${id}`, {
            method: 'DELETE'
        });

        if (response.ok) {
            showToast('Product deleted successfully!', 'success');
            loadProducts();
        } else if (response.status === 401) {
            logout();
        } else {
            showToast('Failed to delete product', 'error');
        }
    } catch (error) {
        showToast('Connection error', 'error', () => deleteProduct(id));
        console.error(error);
    }
}

function resetProductForm() {
    addProductForm.reset();
    document.getElementById('prod-id').value = "";
    formTitle.textContent = 'New Product';
    btnSaveProduct.textContent = 'Save Product';
    btnCancelEdit.classList.add('hidden');
}

// Orders
async function loadOrders() {
    ordersList.innerHTML = '<p class="loading">Loading orders...</p>';
    try {
        const response = await fetchWithAuth(`${API_BASE}/api/orders`);
        if (response.ok) {
            const orders = await response.json();
            renderOrders(orders);
        } else if (response.status === 401) {
            logout();
        } else {
            ordersList.innerHTML = '<p class="error">Failed to load orders.</p>';
        }
    } catch (error) {
        ordersList.innerHTML = '<p class="error">Connection error.</p>';
    }
}

function renderOrders(orders) {
    if (orders.length === 0) {
        ordersList.innerHTML = `
            <div class="text-center py-12">
                <div class="text-6xl mb-4 opacity-50">📋</div>
                <p class="text-slate-500">You have no orders yet.</p>
            </div>
        `;
        return;
    }

    // Sort by date (newest first)
    orders.sort((a, b) => b.id - a.id);

    // Determine status based on order state
    const getStatus = (order) => {
        if (order.status === 'Cancelled' || order.status === 'cancelled') return 'cancelled';
        if (order.status === 'Completed' || order.status === 'completed') return 'completed';
        if (order.status === 'Processing' || order.status === 'processing') return 'processing';
        return 'pending';
    };

    // Format date
    const formatDate = (dateStr) => {
        const date = new Date(dateStr);
        return date.toLocaleDateString('en-US', { 
            year: 'numeric', 
            month: 'short', 
            day: 'numeric' 
        });
    };

    ordersList.innerHTML = orders.map(o => `
        <div class="order-card">
            <div class="order-header">
                <div>
                    <div class="order-id">Order #${o.id}</div>
                    <div class="order-date">${o.createdAt ? formatDate(o.createdAt) : 'Recent'}</div>
                </div>
                <span class="order-status ${getStatus(o)}">${o.status || 'Pending'}</span>
            </div>
            <div class="order-items-summary">
                Product ID: ${o.productId} | Qty: ${o.quantity}
            </div>
            <div class="order-footer">
                <span class="order-total">$${o.totalPrice?.toFixed(2) || '0.00'}</span>
                ${getStatus(o) === 'pending' || getStatus(o) === 'processing' ? 
                    `<button onclick="deleteOrder(${o.id})" class="btn secondary small btn-danger">Cancel</button>` 
                    : ''}
            </div>
        </div>
    `).join('');
}

async function deleteOrder(id) {
    if (!confirm('Are you sure you want to cancel this order?')) return;

    try {
        const response = await fetchWithAuth(`${API_BASE}/api/orders/${id}`, {
            method: 'DELETE'
        });

        if (response.ok) {
            showToast('Order cancelled successfully!', 'success');
            loadOrders();
        } else if (response.status === 401) {
            logout();
        } else {
            showToast('Failed to cancel order', 'error');
        }
    } catch (error) {
        showToast('Connection error', 'error', () => deleteOrder(id));
        console.error(error);
    }
}

// ==================== CART FUNCTIONS ====================

async function loadCart() {
    const userId = getUserId();
    if (!userId) {
        // Not logged in - hide cart
        currentCart = null;
        currentCartId = null;
        updateCartBadge();
        return;
    }

    try {
        const response = await fetchWithAuth(`${API_BASE}/api/carts/${userId}`);
        if (response.ok) {
            currentCart = await response.json();
            currentCartId = currentCart.id;
            updateCartBadge();
            renderCart();
        } else if (response.status === 404) {
            // No cart exists, create one
            currentCart = await createCart();
            updateCartBadge();
            renderCart();
        } else if (response.status === 401) {
            logout();
        }
    } catch (error) {
        console.error('Failed to load cart:', error);
    }
}

async function createCart() {
    const userId = getUserId();
    try {
        const response = await fetchWithAuth(`${API_BASE}/api/carts`, {
            method: 'POST',
            body: JSON.stringify({ userId })
        });
        if (response.ok) {
            return await response.json();
        }
    } catch (error) {
        console.error('Failed to create cart:', error);
    }
    return { id: null, items: [] };
}

async function addToCart(productId, price, quantity = 1) {
    // Validate product
    if (!productId || productId <= 0) {
        showToast('Invalid product', 'error');
        return;
    }
    
    const userId = getUserId();
    if (!userId) {
        showToast('Please login to add items to cart', 'warning');
        return;
    }

    try {
        // First, ensure we have a cart
        if (!currentCartId) {
            currentCart = await createCart();
            currentCartId = currentCart.id;
        }

        const response = await fetchWithAuth(`${API_BASE}/api/carts`, {
            method: 'POST',
            body: JSON.stringify({
                userId,
                productId,
                quantity,
                price
            })
        });

        if (response.ok) {
            showToast('Added to cart!', 'success');
            await loadCart();
        } else if (response.status === 401) {
            logout();
        } else {
            let errorMsg = 'Failed to add to cart';
            try {
                const text = await response.text();
                const errorData = JSON.parse(text);
                errorMsg = errorData.error || errorData.reason || errorMsg;
            } catch (e) {
                // If not JSON, it might just be text
            }
            showToast(errorMsg, 'error');
        }
    } catch (error) {
        showToast('Connection error', 'error', () => addToCart(productId, price, quantity));
        console.error(error);
    }
}

async function updateCartItem(itemId, quantity) {
    if (!currentCartId) return;

    try {
        const response = await fetchWithAuth(`${API_BASE}/api/carts/${currentCartId}/items/${itemId}`, {
            method: 'PUT',
            body: JSON.stringify({ quantity })
        });

        if (response.ok) {
            await loadCart();
        } else if (response.status === 401) {
            logout();
        } else {
            let errorMsg = 'Failed to update quantity';
            try {
                const text = await response.text();
                const errorData = JSON.parse(text);
                errorMsg = errorData.error || errorData.reason || errorMsg;
            } catch (e) {
                // If not JSON, keep default message
            }
            showToast(errorMsg, 'error');
        }
    } catch (error) {
        showToast('Connection error', 'error', () => updateCartItem(itemId, quantity));
        console.error(error);
    }
}

async function removeFromCart(itemId) {
    if (!currentCartId) return;

    try {
        const response = await fetchWithAuth(`${API_BASE}/api/carts/${currentCartId}/items/${itemId}`, {
            method: 'DELETE'
        });

        if (response.ok) {
            showToast('Item removed from cart', 'success');
            await loadCart();
        } else if (response.status === 401) {
            logout();
        } else {
            showToast('Failed to remove item', 'error');
        }
    } catch (error) {
        showToast('Connection error', 'error', () => removeFromCart(itemId));
        console.error(error);
    }
}

function updateCartBadge() {
    const badge = document.getElementById('cart-badge');
    if (!badge) return;

    const itemCount = currentCart?.items?.reduce((sum, item) => sum + item.quantity, 0) || 0;
    badge.textContent = itemCount;
    
    if (itemCount > 0) {
        badge.classList.remove('hidden');
    } else {
        badge.classList.add('hidden');
    }
}

function renderCart() {
    const cartItemsContainer = document.getElementById('cart-items');
    const cartEmpty = document.getElementById('cart-empty');
    const cartSummary = document.getElementById('cart-summary');
    const cartTotalPrice = document.getElementById('cart-total-price');

    if (!currentCart?.items?.length) {
        cartItemsContainer.innerHTML = '';
        cartEmpty.classList.remove('hidden');
        cartSummary.classList.add('hidden');
        return;
    }

    cartEmpty.classList.add('hidden');
    cartSummary.classList.remove('hidden');

    cartItemsContainer.innerHTML = currentCart.items.map(item => `
        <div class="cart-item">
            <div class="cart-item-info">
                <span class="cart-item-name">Product #${item.productId}</span>
                <span class="cart-item-price">$${item.price.toFixed(2)}</span>
            </div>
            <div class="cart-item-actions">
                <div class="quantity-control">
                    <button onclick="updateCartItemQuantity(${item.id}, ${item.quantity - 1})" class="btn-qty">−</button>
                    <span class="quantity-value">${item.quantity}</span>
                    <button onclick="updateCartItemQuantity(${item.id}, ${item.quantity + 1})" class="btn-qty">+</button>
                </div>
                <span class="cart-item-subtotal">$${(item.price * item.quantity).toFixed(2)}</span>
                <button onclick="removeFromCart(${item.id})" class="btn-remove">×</button>
            </div>
        </div>
    `).join('');

    const total = currentCart.items.reduce((sum, item) => sum + (item.price * item.quantity), 0);
    cartTotalPrice.textContent = `$${total.toFixed(2)}`;
}

function updateCartItemQuantity(itemId, newQuantity) {
    if (newQuantity <= 0) {
        removeFromCart(itemId);
    } else {
        updateCartItem(itemId, newQuantity);
    }
}

function openCartModal() {
    cartModal.classList.remove('hidden');
    renderCart();
}

function closeCartModal() {
    cartModal.classList.add('hidden');
}

// ==================== CHECKOUT WIZARD ====================

let checkoutData = {
    total: 0,
    orderId: null,
    transactionId: null
};

function openCheckoutWizard() {
    closeCartModal();
    
    if (!currentCart?.items?.length) {
        showToast('Your cart is empty', 'warning');
        return;
    }

    checkoutData.total = currentCart.items.reduce((sum, item) => sum + (item.price * item.quantity), 0);
    renderCheckoutStep1();
    goToStep(1);
    checkoutModal.classList.remove('hidden');
}

function closeCheckoutModal() {
    checkoutModal.classList.add('hidden');
    // Reset payment form
    document.getElementById('payment-form')?.reset();
}

function goToStep(step) {
    // Hide all steps
    document.querySelectorAll('.wizard-step').forEach(el => {
        el.classList.add('hidden');
        el.classList.remove('active');
    });
    
    // Show target step
    const stepEl = document.getElementById(`checkout-step-${step}`);
    if (stepEl) {
        stepEl.classList.remove('hidden');
        stepEl.classList.add('active');
    }
    
    // Update step indicators
    document.querySelectorAll('.wizard-steps .step').forEach(el => {
        const stepNum = parseInt(el.dataset.step);
        el.classList.toggle('active', stepNum === step);
        el.classList.toggle('completed', stepNum < step);
    });
}

function renderCheckoutStep1() {
    const checkoutItems = document.getElementById('checkout-items');
    const checkoutSubtotal = document.getElementById('checkout-subtotal');
    const checkoutTotal = document.getElementById('checkout-total');

    if (!currentCart?.items?.length) {
        checkoutItems.innerHTML = '<p>No items in cart</p>';
        return;
    }

    checkoutItems.innerHTML = currentCart.items.map(item => `
        <div class="checkout-item">
            <div class="checkout-item-info">
                <span class="checkout-item-name">Product #${item.productId}</span>
                <span class="checkout-item-qty">x${item.quantity}</span>
            </div>
            <span class="checkout-item-price">$${(item.price * item.quantity).toFixed(2)}</span>
        </div>
    `).join('');

    checkoutSubtotal.textContent = `$${checkoutData.total.toFixed(2)}`;
    checkoutTotal.textContent = `$${checkoutData.total.toFixed(2)}`;
}

async function submitPayment() {
    const cardNumber = document.getElementById('card-number').value.replace(/\s/g, '');
    
    if (!cardNumber || cardNumber.length < 13) {
        showToast('Please enter a valid card number', 'warning');
        return;
    }

    if (!currentCartId) {
        showToast('Cart not found', 'error');
        return;
    }

    const submitBtn = document.getElementById('btn-submit-payment');
    submitBtn.disabled = true;
    submitBtn.textContent = 'Processing...';

    try {
        const response = await fetchWithAuth(`${API_BASE}/api/carts/${currentCartId}/checkout`, {
            method: 'POST',
            body: JSON.stringify({ cardNumber }),
            timeout: 30000
        });

        if (response.ok) {
            const result = await response.json();
            checkoutData.orderId = result.orderId;
            checkoutData.transactionId = result.transactionId || result.paymentId || 'N/A';
            
            // Clear cart locally
            currentCart = { id: currentCartId, items: [] };
            updateCartBadge();
            
            // Reload orders
            loadOrders();
            
            renderCheckoutStep3();
            goToStep(3);
            showToast('Payment successful!', 'success');
        } else if (response.status === 401) {
            logout();
        } else if (response.status === 0) {
            // Timeout
            showToast('Payment request timed out. Please try again.', 'warning', submitPayment);
        } else {
            let errorMessage = 'Payment failed';
            try {
                const errorJson = await response.json();
                errorMessage = errorJson.reason || errorJson.error || errorMessage;
            } catch {
                errorMessage = await response.text() || errorMessage;
            }
            showToast(errorMessage, 'error');
        }
    } catch (error) {
        showToast('Connection error', 'error', submitPayment);
        console.error(error);
    } finally {
        submitBtn.disabled = false;
        submitBtn.textContent = 'Pay Now';
    }
}

function renderCheckoutStep3() {
    document.getElementById('confirmation-order-id').textContent = `#${checkoutData.orderId || '---'}`;
    document.getElementById('confirmation-transaction-id').textContent = `#${checkoutData.transactionId || '---'}`;
}

// ==================== SERVICE STATUS ====================

let serviceRefreshInterval = null;

async function checkServicesHealth() {
    servicesList.innerHTML = '<p class="loading">Checking services...</p>';

    const healthChecks = SERVICES_CONFIG.map(async (service) => {
        try {
            const controller = new AbortController();
            const timeoutId = setTimeout(() => controller.abort(), HEALTH_CHECK_TIMEOUT);
            
            const response = await fetch(service.url, {
                method: 'GET',
                signal: controller.signal
            });
            
            clearTimeout(timeoutId);
            
            if (response.ok) {
                return { ...service, status: 'healthy', message: 'OK' };
            } else if (response.status >= 500) {
                return { ...service, status: 'unhealthy', message: `Error ${response.status}` };
            } else {
                return { ...service, status: 'degraded', message: `Warning ${response.status}` };
            }
        } catch (error) {
            if (error.name === 'AbortError') {
                return { ...service, status: 'timeout', message: 'Timeout' };
            }
            return { ...service, status: 'offline', message: 'Offline' };
        }
    });

    const results = await Promise.all(healthChecks);
    
    // Store status for reference
    results.forEach(r => {
        serviceHealthStatus[r.name] = r.status;
    });

    renderServiceStatus(results);
}

function renderServiceStatus(services) {
    servicesList.innerHTML = services.map(service => `
        <div class="service-item" data-service="${service.name}">
            <span class="service-icon">${service.icon}</span>
            <span class="service-name">${service.name.replace('-service', '')}</span>
            <span class="service-indicator ${service.status}"></span>
        </div>
    `).join('');
}

function startServiceStatusRefresh() {
    stopServiceStatusRefresh();
    serviceRefreshInterval = setInterval(checkServicesHealth, SERVICE_REFRESH_INTERVAL);
}

function stopServiceStatusRefresh() {
    if (serviceRefreshInterval) {
        clearInterval(serviceRefreshInterval);
        serviceRefreshInterval = null;
    }
}

// ==================== TOAST NOTIFICATIONS ====================

function showToast(message, type, onRetry = null) {
    const toast = document.createElement('div');
    toast.className = `toast-item ${type}`;
    
    let buttonsHtml = '';
    if (onRetry && (type === 'warning' || type === 'error')) {
        buttonsHtml = `<button class="toast-retry" onclick="this.parentElement.hideToast(); (${onRetry.toString()})();">Retry</button>`;
    }
    
    toast.innerHTML = `
        <span class="toast-message">${escapeHtml(message)}</span>
        <button class="toast-close" onclick="this.parentElement.hideToast();">×</button>
        ${buttonsHtml}
    `;
    
    toast.hideToast = function() {
        toast.classList.add('hiding');
        setTimeout(() => toast.remove(), 300);
    };
    
    toastContainer.appendChild(toast);
    
    // Trigger animation
    requestAnimationFrame(() => {
        toast.classList.add('visible');
    });

    // Auto hide after 5 seconds (only for success, warnings stay longer if no retry)
    const hideDelay = type === 'success' ? TOAST_AUTO_HIDE_DELAY : (onRetry ? 10000 : 8000);
    setTimeout(() => {
        toast.hideToast();
    }, hideDelay);
}

// Backwards compatibility
function showToastOld(msg, type) {
    const toast = document.getElementById('toast');
    toast.textContent = msg;
    toast.className = `toast ${type}`;
    
    requestAnimationFrame(() => {
        toast.className = `toast ${type} visible`;
    });

    setTimeout(() => {
        toast.className = `toast hidden`;
    }, 3000);
}

// ==================== HELPERS ====================

function showMessage(msg, type) {
    authMessage.textContent = msg;
    authMessage.className = `message ${type}`;
}

// Expose functions to global scope for onclick handlers
window.logout = logout;
window.addToCart = addToCart;
window.editProduct = editProduct;
window.deleteProduct = deleteProduct;
window.deleteOrder = deleteOrder;
window.updateCartItem = updateCartItem;
window.updateCartItemQuantity = updateCartItemQuantity;
window.removeFromCart = removeFromCart;
window.submitPayment = submitPayment;
window.goToStep = goToStep;
window.checkServicesHealth = checkServicesHealth;
window.showToast = showToast;

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
let globalProducts = [];

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

    // Back to cart from step 1
    document.getElementById('btn-back-to-cart')?.addEventListener('click', () => {
        closeCheckoutModal();
        openCartModal();
    });

    // Wizard navigation
    document.getElementById('btn-step1-next')?.addEventListener('click', () => {
        // Update payment amount in step 2 before navigating
        const paymentAmountEl = document.getElementById('payment-amount');
        const paymentAmountBtnEl = document.getElementById('payment-amount-btn');
        if (paymentAmountEl) paymentAmountEl.textContent = `$${checkoutData.total.toFixed(2)}`;
        if (paymentAmountBtnEl) paymentAmountBtnEl.textContent = `$${checkoutData.total.toFixed(2)}`;
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

    // Service panel collapse/expand
    const toggleBtn = document.getElementById('btn-panel-collapse');
    const serviceListContainer = document.getElementById('services-list');
    const statusPanelToggle = document.getElementById('status-panel-toggle');
    let isPanelCollapsed = false;

    const togglePanel = () => {
        isPanelCollapsed = !isPanelCollapsed;
        if (serviceListContainer) {
            serviceListContainer.style.maxHeight = isPanelCollapsed ? '0' : '';
            serviceListContainer.style.overflow = isPanelCollapsed ? 'hidden' : '';
        }
        if (toggleBtn) {
            toggleBtn.style.transform = isPanelCollapsed ? 'rotate(180deg)' : '';
        }
    };

    statusPanelToggle?.addEventListener('click', togglePanel);
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
    if (email) {
        const displayName = email.split('@')[0];
        if (userName) userName.textContent = email; // show full email
        // Set avatar initial
        const initialEl = document.getElementById('user-initial');
        if (initialEl) initialEl.textContent = displayName.charAt(0).toUpperCase();
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
            globalProducts = products;
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

// Category color palettes matching V0 design
const CATEGORY_GRADIENTS = [
    'linear-gradient(135deg, #6366f1 0%, #8b5cf6 100%)',   // indigo/purple
    'linear-gradient(135deg, #3b82f6 0%, #6366f1 100%)',   // blue/indigo
    'linear-gradient(135deg, #4f46e5 0%, #7c3aed 100%)',   // deep indigo
    'linear-gradient(135deg, #10b981 0%, #059669 100%)',   // emerald
    'linear-gradient(135deg, #f97316 0%, #ec4899 100%)',   // orange/pink
    'linear-gradient(135deg, #14b8a6 0%, #0d9488 100%)',   // teal
    'linear-gradient(135deg, #8b5cf6 0%, #6366f1 100%)',   // purple/indigo
    'linear-gradient(135deg, #f59e0b 0%, #f97316 100%)',   // amber/orange
    'linear-gradient(135deg, #ef4444 0%, #f97316 100%)',   // red/orange
    'linear-gradient(135deg, #06b6d4 0%, #3b82f6 100%)',   // cyan/blue
];

const CATEGORY_NAMES = [
    'Audio', 'Peripherals', 'Displays', 'Furniture',
    'Smart Home', 'Storage', 'Accessories', 'Cameras',
    'Lighting', 'Networking'
];

function getProductGradient(product) {
    // Use product id to deterministically pick a gradient
    const idx = (product.id - 1) % CATEGORY_GRADIENTS.length;
    return CATEGORY_GRADIENTS[idx];
}

function getProductCategory(product) {
    const idx = (product.id - 1) % CATEGORY_NAMES.length;
    return CATEGORY_NAMES[idx];
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
        const gradient = getProductGradient(p);
        const category = getProductCategory(p);
        
        return `
            <div class="product-card">
                <div class="product-image" style="background: ${gradient}">
                    <span class="product-category-badge">${category}</span>
                    <div class="product-icon-wrapper">
                        <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="1.5">
                            <path stroke-linecap="round" stroke-linejoin="round" d="M21 7.5l-9-5.25L3 7.5m18 0l-9 5.25m9-5.25v9l-9 5.25M3 7.5l9 5.25M3 7.5v9l9 5.25m0-9v9" />
                        </svg>
                    </div>
                    ${p.stock === 0 ? '<div class="product-out-of-stock-overlay"><span>Out of Stock</span></div>' : ''}
                </div>
                <div class="product-content">
                    <h3 class="product-name">${escapeHtml(p.name)}</h3>
                    <p class="product-stock ${stockStatus}">
                        <span class="stock-dot"></span>
                        ${stockText}
                    </p>
                    <div class="product-actions">
                        <p class="product-price">$${p.price.toFixed(2)}</p>
                        <button onclick="addToCart(${p.id}, ${p.price})" class="btn-add-cart" ${p.stock === 0 ? 'disabled' : ''}>
                            <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
                                <path stroke-linecap="round" stroke-linejoin="round" d="M12 4.5v15m7.5-7.5h-15" />
                            </svg>
                            Add to Cart
                        </button>
                    </div>
                    <div class="product-edit-actions">
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
            
            // Si el carrito está abierto o activo, lo recargamos para que el producto desaparezca al instante
            if (currentCartId) {
                loadCart();
            }
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

    // Show orders count
    const ordersCountEl = document.getElementById('orders-count');
    if (ordersCountEl) ordersCountEl.textContent = `${orders.length} orders total`;

    // Sort by date (newest first)
    orders.sort((a, b) => b.id - a.id);

    // Determine status based on order state
    const getStatus = (order) => {
        if (order.status === 'Cancelled' || order.status === 'cancelled') return 'cancelled';
        if (order.status === 'Completed' || order.status === 'completed') return 'completed';
        if (order.status === 'Processing' || order.status === 'processing') return 'processing';
        return 'pending';
    };

    const getStatusLabel = (order) => {
        const s = order.status || 'Pending';
        return s.charAt(0).toUpperCase() + s.slice(1).toLowerCase();
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

    // Generate a short TXN-like code from order id
    const getTxnId = (orderId) => {
        const chars = 'ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789';
        let hash = orderId * 7919;
        let result = 'TXN-';
        for (let i = 0; i < 8; i++) {
            result += chars[hash % chars.length];
            hash = Math.floor(hash / chars.length) + orderId;
        }
        return result.substring(0, 12);
    };

    // Build order ID display (ORD-XXXXX format)
    const getOrderIdDisplay = (id) => `ORD-${String(id).padStart(5, '0')}`;

    // Determine product names from globalProducts
    const getItemsHtml = (order) => {
        if (order.items && order.items.length) {
            return order.items.map(item => {
                const product = globalProducts.find(p => p.id == item.productId);
                const name = product ? escapeHtml(product.name) : `Product #${item.productId}`;
                return `<div class="order-item-row">
                    <svg class="order-item-icon" xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="1.5">
                        <path stroke-linecap="round" stroke-linejoin="round" d="M21 7.5l-9-5.25L3 7.5m18 0l-9 5.25m9-5.25v9l-9 5.25M3 7.5l9 5.25M3 7.5v9l9 5.25m0-9v9" />
                    </svg>
                    <span>${name}</span>
                    <span class="order-item-qty">x${item.quantity}</span>
                </div>`;
            }).join('');
        }
        // Legacy: single product from order fields
        const product = globalProducts.find(p => p.id == order.productId);
        const name = product ? escapeHtml(product.name) : `Product #${order.productId}`;
        return `<div class="order-item-row">
            <svg class="order-item-icon" xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="1.5">
                <path stroke-linecap="round" stroke-linejoin="round" d="M21 7.5l-9-5.25L3 7.5m18 0l-9 5.25m9-5.25v9l-9 5.25M3 7.5l9 5.25M3 7.5v9l9 5.25m0-9v9" />
            </svg>
            <span>${name}</span>
            <span class="order-item-qty">x${order.quantity || 1}</span>
        </div>`;
    };

    ordersList.innerHTML = orders.map(o => `
        <div class="order-card">
            <div class="order-header">
                <div class="order-header-left">
                    <div class="order-id-row">
                        <span class="order-id">${getOrderIdDisplay(o.id)}</span>
                        <span class="order-status ${getStatus(o)}">
                            <span class="order-status-dot"></span>
                            ${getStatusLabel(o)}
                        </span>
                        ${getStatus(o) === 'pending' || getStatus(o) === 'processing' ? 
                            `<button onclick="deleteOrder(${o.id})" class="btn-cancel-order">Cancel</button>` 
                            : ''}
                    </div>
                    <div class="order-txn-id">${getTxnId(o.id)}</div>
                </div>
                <div class="order-header-right">
                    <span class="order-total">$${o.totalPrice?.toFixed(2) || '0.00'}</span>
                    <span class="order-date">${o.createdAt ? formatDate(o.createdAt) : 'Recent'}</span>
                </div>
            </div>
            <div class="order-items-list">
                ${getItemsHtml(o)}
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

    cartItemsContainer.innerHTML = currentCart.items.map(item => {
        const product = globalProducts.find(p => p.id == item.productId);
        const name = product ? escapeHtml(product.name) : `Product #${item.productId}`;
        const gradient = product ? getProductGradient(product) : 'linear-gradient(135deg, #6366f1 0%, #8b5cf6 100%)';
        return `
        <div class="cart-item">
            <div class="cart-item-thumb" style="background: ${gradient}">
                <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="1.5">
                    <path stroke-linecap="round" stroke-linejoin="round" d="M21 7.5l-9-5.25L3 7.5m18 0l-9 5.25m9-5.25v9l-9 5.25M3 7.5l9 5.25M3 7.5v9l9 5.25m0-9v9" />
                </svg>
            </div>
            <div class="cart-item-info">
                <span class="cart-item-name">${name}</span>
                <span class="cart-item-price">$${item.price.toFixed(2)}</span>
            </div>
            <div class="cart-item-actions">
                <div class="quantity-control">
                    <button onclick="updateCartItemQuantity(${item.id}, ${item.quantity - 1})" class="btn-qty">−</button>
                    <span class="quantity-value">${item.quantity}</span>
                    <button onclick="updateCartItemQuantity(${item.id}, ${item.quantity + 1})" class="btn-qty">+</button>
                </div>
                <button onclick="removeFromCart(${item.id})" class="btn-remove" title="Remove item">
                    <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
                        <path stroke-linecap="round" stroke-linejoin="round" d="M14.74 9l-.346 9m-4.788 0L9.26 9m9.968-3.21c.342.052.682.107 1.022.166m-1.022-.165L18.16 19.673a2.25 2.25 0 01-2.244 2.077H8.084a2.25 2.25 0 01-2.244-2.077L4.772 5.79m14.456 0a48.108 48.108 0 00-3.478-.397m-12 .562c.34-.059.68-.114 1.022-.165m0 0a48.11 48.11 0 013.478-.397m7.5 0v-.916c0-1.18-.91-2.164-2.09-2.201a51.964 51.964 0 00-3.32 0c-1.18.037-2.09 1.022-2.09 2.201v.916m7.5 0a48.667 48.667 0 00-7.5 0" />
                    </svg>
                </button>
            </div>
        </div>
        `;
    }).join('');

    const total = currentCart.items.reduce((sum, item) => sum + (item.price * item.quantity), 0);
    cartTotalPrice.textContent = `$${total.toFixed(2)}`;

    // Update cart header count badge
    const cartHeaderCount = document.getElementById('cart-header-count');
    const itemCount = currentCart.items.reduce((sum, item) => sum + item.quantity, 0);
    if (cartHeaderCount) {
        cartHeaderCount.textContent = `${itemCount} Item${itemCount !== 1 ? 's' : ''}`;
        cartHeaderCount.classList.remove('hidden');
    }
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

    checkoutItems.innerHTML = currentCart.items.map(item => {
        const product = globalProducts.find(p => p.id == item.productId);
        const name = product ? escapeHtml(product.name) : `Product #${item.productId}`;
        const gradient = product ? getProductGradient(product) : 'linear-gradient(135deg, #6366f1 0%, #8b5cf6 100%)';
        return `
        <div class="checkout-item">
            <div class="checkout-item-thumb" style="background: ${gradient}">
                <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="1.5">
                    <path stroke-linecap="round" stroke-linejoin="round" d="M21 7.5l-9-5.25L3 7.5m18 0l-9 5.25m9-5.25v9l-9 5.25M3 7.5l9 5.25M3 7.5v9l9 5.25m0-9v9" />
                </svg>
            </div>
            <div class="checkout-item-info">
                <span class="checkout-item-name">${name}</span>
                <span class="checkout-item-qty">Qty: ${item.quantity}</span>
            </div>
            <span class="checkout-item-price">$${(item.price * item.quantity).toFixed(2)}</span>
        </div>
        `;
    }).join('');

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
    const onlineCount = services.filter(s => s.status === 'healthy').length;
    const total = services.length;
    const allHealthy = onlineCount === total;

    // Update panel header count
    const countEl = document.getElementById('service-count');
    if (countEl) {
        countEl.textContent = `${onlineCount}/${total}`;
        countEl.className = allHealthy ? 'service-count healthy' : 'service-count degraded';
    }

    // Update the panel indicator dot
    const panelDot = document.getElementById('service-panel-dot');
    if (panelDot) {
        panelDot.className = allHealthy ? 'service-panel-dot healthy' : 'service-panel-dot degraded';
    }

    const getStatusLabel = (status) => {
        if (status === 'healthy') return 'Online';
        if (status === 'degraded') return 'Degraded';
        if (status === 'timeout') return 'Timeout';
        return 'Down';
    };

    const getServiceDisplayName = (name) => {
        const map = {
            'product-service': 'Product API',
            'order-service': 'Order Service',
            'cart-service': 'Cart Service',
            'payment-service': 'Payment Gateway',
            'notification-service': 'Notification Service',
            'inventory-service': 'Auth Service',
        };
        return map[name] || name.replace('-service', ' Service');
    };

    servicesList.innerHTML = services.map(service => `
        <div class="service-item" data-service="${service.name}">
            <span class="service-name">${getServiceDisplayName(service.name)}</span>
            <span class="service-status-label ${service.status}">${getStatusLabel(service.status)}</span>
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

# E-Commerce Microservices Frontend - v0.dev Prompt

Create a modern e-commerce SPA frontend for a microservices online shop. This is a single-page application with authentication and shopping cart functionality.

## Pages/Views

### 1. Login/Register
- Centered card with toggle between Login and Register modes
- Email and password inputs with floating labels
- Gradient submit button
- Logo and app name at top

### 2. Dashboard (Main Shop)
- Sticky header with:
  - Logo (gradient icon "M" + "MicroShop" text)
  - Navigation tabs (Products, My Orders)
  - Cart icon with badge counter
  - Logout button
- Product grid (3 columns desktop, 2 tablet, 1 mobile)
- Product cards with:
  - Gradient placeholder image
  - Product name, price, stock
  - "Add to Cart" button with cart icon
  - Hover: lift up + shadow increase

### 3. Cart Modal
- Slide-in from right
- Item list with:
  - Product image thumbnail
  - Name and price
  - Quantity +/- buttons in circles
  - Remove button
- Sticky footer with:
  - Total amount (large, primary color)
  - "Proceed to Checkout" button (full width, gradient)

### 4. Checkout Wizard (3 Steps)
- Progress bar at top with 3 steps:
  1. 📋 Review Cart
  2. 💳 Payment
  3. ✅ Confirmation
- Step indicators connected by line
- Step 1: Cart summary, subtotal/total
- Step 2: Card number input, test cards hint, amount to pay, "Pay Now" button
- Step 3: Success checkmark animation, order ID, transaction ID, "Continue Shopping" button

### 5. My Orders
- Orders list as cards
- Each order shows:
  - Order ID and date
  - Items summary
  - Total price
  - Status badge (green "Completed", yellow "Pending", blue "Processing", red "Cancelled")

### 6. Service Status Panel
- Fixed position bottom-right
- Floating card with shadow
- Grid of services with:
  - Colored dot indicator (green/red/yellow)
  - Pulsing animation for healthy services
  - Service name

### 7. Toast Notifications
- Fixed bottom-center
- Rounded pill shape
- Success (green), Error (red), Warning (yellow) variants
- Auto-dismiss with fade animation

## Design System

### Colors
- Primary: Indigo (#6366f1)
- Secondary: Violet (#8b5cf6)
- Success: Emerald (#22c55e)
- Warning: Amber (#f59e0b)
- Error: Red (#ef4444)
- Background: Gray-50 (#f9fafb)
- Surface: White (#ffffff)
- Text: Gray-900 (#111827)
- Text muted: Gray-500 (#6b7280)

### Typography
- Font: Inter, system-ui, sans-serif
- Headings: Bold, gray-900
- Body: Regular, gray-600
- Prices: Bold, primary color

### Spacing & Shapes
- Border radius: rounded-xl (12px) for cards, rounded-lg (8px) for buttons
- Shadows: shadow-sm default, shadow-lg on hover
- Padding: p-4 to p-6

### Animations
- Transitions: 200-300ms ease
- Hover lift: -translate-y-1
- Modal slide: translate-x with spring easing
- Loading pulse on status dots

## Tech Stack
- Vanilla JavaScript (NO React/Vue)
- HTML5 with Tailwind CSS classes
- CSS custom animations only where Tailwind doesn't cover

## API Integration (fetch)
All API calls use fetch() to `/api/*` endpoints:
- POST /api/auth/login - Returns { token }
- POST /api/auth/register
- GET /api/products
- GET/POST /api/carts/{userId}
- POST /api/carts/{cartId}/checkout
- GET /api/orders

## Components to Build
- Header with navigation
- Product card
- Cart item row
- Checkout wizard
- Order card
- Service status badge
- Toast notification
- Auth form (login/register)
- Modal overlay

## Notes
- Mobile-first responsive design
- All buttons must have hover states
- Loading states for async operations
- Error handling with toast messages
- Empty states for cart and orders

# UI/UX & Design Standards

## Visual Excellence & Aesthetics
1. **Modern Design Patterns**: Always use modern design patterns (glassmorphism, clean typography, whitespace).
2. **Color Palettes**: Avoid generic colors (plain red, blue, green). Use curated HSL palettes, vibrant gradients, and sleek dark modes.
3. **Typography**: Use modern sans-serif fonts (Inter, Roboto, Outfit) instead of browser defaults. Establish a clear visual hierarchy using weight and size.

## Animation & Motion
1. **Micro-interactions**: Every interactive element (buttons, cards, links) MUST have a hover and active state with smooth transitions (e.g., `transition: all 0.2s ease-in-out`).
2. **Entry Animations**: Use stagger animations for lists and fade-in-up for page content to make interfaces feel alive.
3. **Physics**: When animating, prefer spring physics or custom cubic-bezier curves over linear easings.

## Accessibility (a11y)
1. Ensure sufficient color contrast (WCAG AA).
2. All interactive elements must be keyboard accessible and have proper `aria-` labels.
3. Support reduced motion preferences (`@media (prefers-reduced-motion: reduce)`).

## Implementation Rules
- Start by creating a centralized `index.css` or design token system.
- Build components iteratively, ensuring they match the defined aesthetic.
- Never use placeholders if you can generate a demonstration image using the `generate_image` tool.

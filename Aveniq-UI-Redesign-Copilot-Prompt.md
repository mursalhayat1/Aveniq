# GitHub Copilot Prompt: Aveniq Premium UI Redesign

Copy the prompt below into GitHub Copilot Chat while the Aveniq repository is open.

---

## Prompt

You are a senior Windows desktop UI/UX designer and C# WPF engineer. Redesign the Aveniq application UI to feel premium, modern, polished, and production-ready, closely matching the supplied visual reference.

### Project and reference

- This is **Aveniq**, a C# desktop application built with **.NET 10 WPF**, **XAML**, and an **MVVM** architecture. It is not a web app, WinForms app, or a request to replace the desktop shell with a browser UI.
- The design reference is `C:\Users\Mursal Hayat\Downloads\Documents\Aveniq app redesign.html`. Inspect the reference itself and all eight embedded screens before changing the app. The screens are Dashboard, Cloud Drives, Game Library, Game Accelerator, Smart Cache, Performance Monitor, Settings, and Diagnostics.
- The reference boards are designed at approximately 1360 × 780. Treat the appearance of the actual screens inside the boards as the visual source of truth; do not mistake the surrounding mockup canvas or its margins for the application surface.
- Start by inspecting `Aveniq.slnx`, `src\Aveniq.Desktop\Aveniq.Desktop.csproj`, `MainWindow.xaml`, `Themes\Theme.xaml`, the eight corresponding page views, and their view models. Trace existing bindings, commands, converters, navigation, and reusable resources before editing.

### Design goals

Create a cohesive visual system across the shell, all eight screens, and related dialogs:

- Match the reference's composition, visual hierarchy, spacing, proportions, surface treatments, and overall mood. Where the existing theme differs from the reference, derive the colors from the reference rather than blindly retaining the old palette. Define the chosen palette as reusable WPF resources and use it consistently.
- Use refined typography with a clear type scale: confident page titles, legible section headings, readable body text, quiet secondary labels, and well-aligned numeric metrics. Prefer the reference's typography; use available Windows fonts such as Segoe UI Variable as a robust fallback. Use a monospaced face only where it improves technical values or diagnostics.
- Build a restrained, harmonious palette with strong text/background contrast. Use accent colors intentionally for primary actions, selection, focus, links, and meaningful status. Keep success, warning, error, and neutral states distinguishable without relying on color alone.
- Use modern, consistent vector-style icons. Prefer icons already available in the project or Windows Fluent/MDL2 icon resources; do not use emoji, arbitrary Unicode glyphs, or a mixture of unrelated icon styles.
- Use intentional whitespace, consistent alignment, subtle borders and elevation, carefully tuned corner radii, and clear grouping. Avoid visual clutter, gratuitous gradients, excessive shadows, tiny labels, and dense walls of controls.
- Make layouts adapt gracefully to the existing resizable desktop window. Use WPF `Grid` sizing, shared spacing resources, scroll behavior, and sensible minimum widths rather than hard-coded coordinates or layouts that clip when resized. Preserve a comfortable layout at the reference size and at the app's supported minimum window size.
- Provide polished interaction states for hover, pressed, selected, disabled, loading, empty, success, and error states. Ensure keyboard focus is visible and navigation is usable with the keyboard. Keep text readable at normal Windows display scaling.

### Screens to redesign

Apply the reference consistently to each of these existing screens:

1. **Dashboard** — clear at-a-glance status and primary metrics, with prominent, readable information hierarchy.
2. **Cloud Drives** — polished connected-provider and mount status presentation, with clear connect/add and management actions.
3. **Game Library** — attractive, scannable game presentation and obvious library actions.
4. **Game Accelerator** — clear acceleration state, impact, and available actions without obscuring operational details.
5. **Smart Cache** — understandable cache usage, status, and controls with legible progress and data presentation.
6. **Performance Monitor** — clean, information-dense performance metrics and charts with labeled units and useful legends.
7. **Settings** — well-grouped, easy-to-scan settings sections with consistent controls and explanatory text.
8. **Diagnostics** — clear system health, dependency, and diagnostic information with obvious status and next steps.

Use only information and actions that already exist in the code or are shown in the reference. Do not invent operational metrics, capabilities, settings, provider states, or success messages. Where data is missing or unavailable, retain the app's existing empty/loading/error behavior and present it clearly.

### Implementation requirements

- Implement the redesign in the existing WPF application. Make the necessary **XAML and C# changes**; use CSS only if the repository contains a real embedded web surface that actually needs it.
- Preserve current MVVM boundaries, view-model responsibilities, data bindings, commands, navigation, provider integrations, validation, async behavior, accessibility semantics, and business logic. Reuse existing controls, converters, models, and services where practical.
- Prefer reusable WPF styles, templates, brushes, typography, spacing, and icon resources over duplicating visual properties across pages. Keep resource keys and ownership clear, and avoid creating an unnecessary design-system framework.
- Keep existing user-facing features functional, including cloud-drive connection and management flows. Do not replace working buttons with decorative placeholders, add fake data, or silently swallow errors.
- Do not add packages, remote assets, or runtime network dependencies solely for visual polish. Do not introduce a second UI framework.
- Keep changes scoped to the UI redesign. Do not rename public APIs, change storage formats, alter provider behavior, or remove existing functionality.

### Workflow and deliverables

1. First summarize the existing UI structure, the reference's shared visual language, and the files you expect to change. Then implement the redesign; do not stop at a design proposal or sample screen.
2. Apply the redesign across the shared shell and all eight screens so they feel like one application, not eight unrelated mockups. Update relevant dialogs only where needed for consistent styling and usability.
3. Provide the concrete implementation, including the actual XAML and any necessary C# snippets or file edits. Do not answer with pseudocode or isolated snippets that cannot be integrated into this repository.
4. Build the solution and run the relevant tests. Fix any regressions caused by the redesign. If a check cannot be run, state exactly which check and why.
5. Finish with a concise summary of the design decisions, changed files, build/test results, and any remaining limitations.

### Acceptance criteria

- All eight listed screens and the shared application shell follow the supplied reference's premium visual direction.
- Typography, color, spacing, icons, cards/surfaces, and interaction states are consistent and readable.
- Layouts resize without overlapping or clipping at supported window sizes.
- Existing commands, navigation, bindings, data, and application behavior remain intact.
- The application builds, relevant tests pass, and the final response identifies what changed and how it was verified.

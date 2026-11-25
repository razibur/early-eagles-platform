// Element-level touches for Clerk's components (P0-039.3). Colors, font and
// radius come from the --clerk-* properties in globals.css; this only swaps
// Clerk's soft drop shadow for the hairline ring the app's cards use, and
// gives headings the display face.
export const clerkAppearance = {
  elements: {
    cardBox: "w-full max-w-[25rem] shadow-none ring-1 ring-rule rounded-lg",
    card: "shadow-none bg-card",
    footer: "bg-paper! [&_*]:bg-transparent!",
    headerTitle: "font-display! uppercase tracking-tight text-2xl! font-extrabold!",
    formButtonPrimary: "uppercase tracking-[0.14em] text-xs font-bold",
  },
}

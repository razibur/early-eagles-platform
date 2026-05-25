# ADR-0005: Light social layer — photo wall, reactions, announcement comments

- **Status:** Accepted
- **Date:** 2026-05-25
- **Deciders:** Project owner (Moh. Razib)
- **Related docs:** [`Plan.md`](../../Plan.md), [`Implementation-Plan.md`](../../Implementation-Plan.md), [`PROGRESS.md`](../../PROGRESS.md), [ADR-0002](./0002-enterprise-saas-reframe.md), [ADR-0004](./0004-role-based-landing-and-routing.md)

## Context

The original [`Plan.md`](../../Plan.md) framed EarlyEagles as a **club operations tool** — RSVP, attendance, dues, events, organised chat. Members come in, do their thing, leave. The implicit engagement assumption is *weekly-open* (Sunday morning RSVP), not *daily-open*.

The project owner questioned whether that engagement floor is enough — members would still default to WhatsApp for everything social, and the app risks being opened once a week then ignored. The conversation surfaced three honest positions:

1. **Ops-only** (current Plan.md) — low engagement ceiling, lowest scope, lowest moderation risk.
2. **Full community space** — feed, free-form text posts, member DMs, stories, reactions on everything; daily-open habit but ~60–80 extra tasks plus ongoing moderation infrastructure. Effectively a competitor to Facebook Groups.
3. **Light social layer** — a contained set of community features that give members a reason to open the app between Sundays, without the moderation surface of (2).

The owner picked (3). This ADR scopes exactly what (3) means so it does not drift into (2) by accretion.

A separate clarification: **DMs are not new scope.** [`Implementation-Plan.md`](../../Implementation-Plan.md) Phase 3 already includes `P3-004 DM channel creation between members` and `P3-009 Web Push for mentions + DMs`. The "light social layer" is purely additive on top of the existing chat scope.

## Decision

The following surfaces are added to the plan, all phased to land **after Phase 3 chat is live**:

### 1. Club photo wall — Phase 4

- Global, chronological photo feed scoped to the tenant.
- Members can upload a photo at any time (not just attached to an event).
- A photo may optionally be tagged to an event; tagged photos appear in both the wall and the event's gallery.
- Admin can delete any photo. Uploader can delete their own.
- **Out of scope:** captions-as-stories, photo editing tools, filters, image AI manipulation, photo albums beyond event grouping.

### 2. Reactions — implementation across Phase 3 + Phase 4

The `Reaction` entity already exists in the Phase 3 schema (`P3-002`). What is added is the **implementation and the cross-feature primitive**:

- Slack-style emoji reactions on chat messages, photos, and announcements.
- Allowed emoji set is a fixed, small, badminton-and-community-coded set (e.g., 👍 🔥 🏸 ✅ ❤️). Tenant-admins cannot add custom emoji in this phase (defer to Phase 7+ enterprise customisation).
- Aggregate counts + "who reacted" hover/tap.
- **Out of scope:** custom emoji, reaction-based polls, reaction analytics.

### 3. Announcement comments — Phase 4

- A single flat thread of replies under each admin announcement.
- Members can reply N times (configurable, default unlimited).
- **Out of scope:** nested replies, reply-to-comment, threaded conversations, comment edits beyond a short window, comment reactions (one layer is enough — comments and reactions don't compound).

## Explicitly NOT in scope (so this doesn't quietly become option 2)

- ❌ Free-form text posts — there is no "What's on your mind, Razi?" composer anywhere in the app.
- ❌ Member status updates / stories / ephemeral content.
- ❌ A timeline / activity feed ("Razi paid May dues", "5 new RSVPs"). Audit logs exist for compliance — they are not a member-facing stream.
- ❌ Member-to-member DMs as a *new* surface — DMs were already planned in Phase 3 (P3-004) and remain as-is. This ADR does not expand or contract them.
- ❌ Heavy moderation tooling — keyword filters, content reports, automated NSFW detection, ban management workflow. Trust the small-tenant community model; revisit at Phase 7 enterprise tier if a tenant requests it.
- ❌ Push notifications on every reaction. Push fires on: new photo posted (opt-in per member), new announcement, new comment on an announcement *you* commented on. That's it.

## Estimated cost

~16 new tasks split:

- **Phase 3** grows from 15 → 19 tasks (4 new: reactions API, reactions UI on chat, aggregate denormalisation, reactions become a shared component primitive).
- **Phase 4** grows from 20 → 32 tasks (12 new: photo wall entity generalisation, feed endpoint, feed UI, upload UX, photo detail, reactions on photos, announcement comment entity, comment endpoints, comment UI, notifications policy, moderation surface, E2E tests).

Specific task IDs land in [`Implementation-Plan.md`](../../Implementation-Plan.md) under Phase 3 (`P3-016` → `P3-019`) and Phase 4 (`P4-021` → `P4-032`).

## Consequences

### Engagement upside
- Daily-open habit becomes plausible: a member opens the app to scroll the wall the same way they'd open Instagram. Once there, they see the dues badge.
- Replaces a real WhatsApp behaviour (sharing photos after games) — concrete migration story.

### Build cost
- ~16 tasks across Phase 3–4. Real but bounded. Does **not** delay Phase 0 or Phase 1, which remain the priorities.

### Moderation cost
- A photo wall is a content surface. The trust model — small club, members know each other, admin can delete anything — covers the expected risk for Tenant 0 and similar small-tenant clubs. The risk grows with larger Pro/Enterprise tenants; revisit moderation tooling in Phase 7.
- No NSFW auto-moderation. Acceptable for trusted-club use; would not be acceptable for a public photo-sharing product.

### Storage cost
- Photo wall reuses the Cloudflare R2 bucket already required for event galleries (P0-007). No new infra.

### Notification cost
- Web Push is already scoped (P0-068, P3-009). Photo + comment notifications reuse the same delivery path. Tenant-admin can configure default opt-in/opt-out per category.

### Risks
- **Scope drift into option 2.** Each one of "let users caption photos / let users reply to comments / let users react to comments" is small individually but compounds. **Mitigation:** the explicit "NOT in scope" list above. Any addition requires a superseding ADR.
- **Wall goes quiet.** If members don't upload photos, the wall reads as broken. **Mitigation:** seed-upload from any existing event photos at the moment the wall ships; treat the first month of low activity as expected, not as failure.

### Reversibility
- Removing photo wall / comments / reactions is straightforward; both are additive features sitting beside chat. No data migration required to roll back, only deprecation + cleanup. Medium cost to reverse.

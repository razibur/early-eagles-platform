# Error budgets and alerting thresholds (P0-094)

What "reliable enough" means, how much unreliability we can afford in a month, and which signals
wake someone up. The alert rules themselves are built with the dashboards (P0-091) and uptime checks
(P0-092). This page is the spec they implement.

## 1. Service level objectives

Measured per calendar month, per environment, on **prod** only.

| SLO | Target | Measured as | Budget per 30 days |
|---|---|---|---|
| **Availability** | 99.5% | share of minutes the external uptime check (`/ready`, P0-092) passes | ~3 h 36 min down |
| **Request success** | 99.5% | API requests not answered with 5xx (App Insights `AppRequests`, probes excluded) | 1 failed request in 200 |
| **Latency** | 95% of API reads under 500 ms | `AppRequests` duration, GET only | 5% of reads may be slower |
| **Sunday window** | 99.9% | availability **Sundays 06:00–11:00 US Eastern**, when Tenant 0 plays | ~1 min per Sunday |
| **Event delivery** | 99% of domain events published within 5 min | age of the oldest pending `outbox_messages` row | — |

Why 99.5% and not more: one region, Consumption Container Apps scaled to zero outside prod, Burstable
Postgres without HA. All of that is the deliberate minimum-cost posture (infra-conventions §7).
Promising more than the architecture delivers would make the budget meaningless. Raise the targets
when Enterprise tenants pay for HA, and write it down when you do.

## 2. Spending the budget

| Budget left this month | What it means |
|---|---|
| > 50% | Ship normally. |
| 25–50% | Ship, but no risky infrastructure changes on a Friday or a Saturday. |
| < 25% | Reliability work comes first; feature releases need a rollback plan written in the PR. |
| Exhausted | Feature freeze until the month resets or the cause is fixed. Write up what consumed it. |

Planned maintenance counts against the budget. Pick Tuesday–Thursday, never the Sunday window.

## 3. Alerts

Two kinds only. **Page** = act now, any hour (Sev 1/2 in [`incident-response.md`](./incident-response.md)).
**Ticket** = look within a working day. Nothing else notifies anyone; dashboards are for looking, not
for alerting.

| Signal | Threshold | Kind | Source |
|---|---|---|---|
| Uptime check on `/ready` fails | 2 consecutive checks (3-min interval), from 2 locations | **Page** | Better Stack (P0-092) |
| Availability budget burn, fast | 14× the allowed rate over 1 h **and** over 5 min (~2% of the month's budget per hour) | **Page** | App Insights log alert |
| Availability budget burn, slow | 6× over 6 h **and** over 30 min | Ticket | App Insights log alert |
| 5xx rate | > 5% of requests over 10 min, at least 20 requests | **Page** | App Insights log alert |
| Read latency | p95 > 1 s over 15 min | Ticket | App Insights log alert |
| Outbox backlog | oldest pending event older than 10 min | Ticket | log alert on the outbox processor's logs |
| Any request touching two tenants' data | any occurrence | **Page** | isolation is enforced in code; this is the regression signal from `CrossTenantWriteException` logs |
| Postgres CPU | > 80% for 15 min | Ticket | Azure Monitor metric alert |
| Postgres storage | > 80% used | Ticket | Azure Monitor metric alert |
| Log Analytics daily cap reached | any day | Ticket | Azure Monitor (the cap silently drops logs) |
| `ghcr.io` pull token expiry | 14 days before | Ticket | calendar reminder ([`deploy-infra.md`](./deploy-infra.md) §10) |

**Burn rates** follow the standard multi-window method: a page needs both a long window (it's real) and
a short window (it's still happening), so a spike that already ended doesn't wake anyone.

**Cost.** Metric alert rules cost cents a month each. Log-search alerts cost more per rule and per
evaluation frequency, so the table keeps them to a handful at 5–15 minute frequencies. Better Stack's
free tier covers the uptime checks and the status page. Adding an alert is a cost decision like any
other resource (as-free-as-possible posture).

## 4. Review

Monthly, five minutes: budget used per SLO, alerts that fired, and for each one whether it was
actionable. An alert that fired and needed no action gets fixed or deleted the same day.

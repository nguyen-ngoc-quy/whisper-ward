# Epic: Guard AI Coordinated Alert & Radio Bark Network

> **Layer**: AI & Navigation  
> **GDD Reference**: `design/gdd/guard-ai-fsm.md` (Coordinated Alert, Alert Propagation C3, lines 307 & 495)  
> **Governing ADRs**: `ADR-0001` (Event Bus), `ADR-0007` (NavMesh Pathfinding)  
> **Status**: Complete  
> **Stories**: 2 stories (`ALERT-01` in Sprint 04, `ALERT-02` in Sprint 04)  

## Overview

The Coordinated Alert epic implements inter-guard communication via radio bark broadcasts and coordinated multi-guard search behavior. When a guard initiates Chase on the player, they broadcast an acoustic radio alert over a $25.0\text{ m}$ radius. Nearby patrolling guards receive the bark and redirect their routes to investigate the reported Last Known Position (LKP) with a spatial error offset ($r_{\text{investigate\_error}} = 1.50\text{ m}$). Guards actively engaged in Chase are immune to peer alerts per the Chase-wins rule (R13). When multiple guards arrive at the same LKP, `MultiGuardSearchCoordinator` allocates standoff positions separated by $\ge 2.0\text{ m}$ and assigns divergent look-around scan angles ($180^\circ$ opposite arcs for 2 guards) during dwell search.

## Stories

| # | Story | Type | Status | Owner |
|---|-------|------|--------|-------|
| `ALERT-01` | Guard Radio Bark & Coordinated Alert | Logic/Integration | Complete | ai-programmer |
| `ALERT-02` | Multi-Guard Search Standoff Coordination | Logic/Integration | Complete | ai-programmer |

## Definition of Done

This epic is complete when:
- Guard transitioning to Chase broadcasts `GuardAlertEvent` with player LKP.
- Radio bark propagation covers $25.0\text{ m}$ radius with a $5.0\text{ s}$ rate-limit cooldown.
- Patrolling/unalerted guards within range redirect to Investigate at LKP with $r \le 1.50\text{ m}$ error offset.
- Guards in Chase state ignore incoming peer alerts (R13 Chase-wins).
- Multiple guards investigating the same LKP maintain $\ge 2.0\text{ m}$ separation and divergent scan arcs.
- Automated tests verify broadcast triggers, reception distance bounds, cooldown rate-limiting, Chase immunity, standoff distances, and divergent dwell angles.

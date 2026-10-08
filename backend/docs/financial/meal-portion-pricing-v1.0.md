# Meal Portion Pricing Policy — v1.0

**Status:** Production design  
**Effective:** 2026-10-08  
**Scope:** Standalone SaaS outlet meal/recipe pricing

## 1. Purpose

Document how Regular and Large meal portions affect customer pricing and how outlet configuration controls availability.

## 2. Outlet configuration

Each outlet has a persisted `SupportsLargePortion` setting.

- `true`: the outlet may offer Regular and Large portions.
- `false`: the outlet offers Regular portions only.
- Existing outlets default to `true` for backward compatibility with previously configured Large prices.
- The outlet administrator can change this setting from **Settings → Customer package configuration**.

## 3. Recipe pricing

Every recipe stores:

- Regular price: `PricePerMeal`
- Large price: `LargePricePerMeal`

When `SupportsLargePortion=false`, new or edited recipes have their Large price suppressed by the application and customer-facing APIs return Large price as zero.

## 4. Customer/package calculation

Regular portion uses `PricePerMeal`.

Large portion uses `LargePricePerMeal` only when the outlet supports Large portions and a positive Large price exists.

The existing meal pricing strategy safely falls back to Regular pricing if a Large price is unavailable. The UI also hides Large selection when the outlet does not offer it.

## 5. Nutrition

Recipe nutrition is calculated independently for Regular and Large ingredient quantities. Large nutrition is exposed only when Large portions are enabled.

Ingredient nutrition must come from the ingredient master/reference data. The application does not silently invent missing nutrition values; missing references are surfaced to outlet staff.

## 6. Audit / change control

Changing `SupportsLargePortion` is an outlet commercial configuration change. It can affect the choices available to customers and therefore must be treated as a pricing/product configuration change.

No historical customer transaction is recalculated solely because the outlet changes this setting. Historical transactions retain their recorded amounts.

## 7. Version history

| Version | Date | Change |
|---|---|---|
| 1.0 | 2026-10-08 | Initial production rule for outlet-configurable Regular/Large portions, pricing, nutrition and backward compatibility. |

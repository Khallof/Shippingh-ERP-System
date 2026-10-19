import type { ShipmentPackageRequest } from '@/api/generated';

/**
 * Splits the total weight over the packages in grams, so the package weights always add up exactly to the total
 * (e.g. 10 kg / 3 = 3.334 + 3.333 + 3.333). The API needs one row per package for the barcodes and labels.
 */
export function splitPackages(count: number, totalWeightKg: number): ShipmentPackageRequest[] {
  const grams = Math.round(totalWeightKg * 1000);
  const base = Math.floor(grams / count);
  const remainder = grams - base * count;
  return Array.from({ length: count }, (_, index) => ({
    description: null,
    weightKg: (base + (index < remainder ? 1 : 0)) / 1000,
    lengthCm: null,
    widthCm: null,
    heightCm: null
  }));
}

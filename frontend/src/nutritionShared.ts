import type { MealSlot } from './types';

// Shared by the meal book and the food log so both read the same way.

export const SLOTS: MealSlot[] = ['Breakfast', 'Lunch', 'Dinner', 'Snack'];

// One glyph per slot so the tabs are found by shape before they are read.
export const SLOT_EMOJI: Record<MealSlot, string> = {
  Breakfast: '🍳',
  Lunch: '🥗',
  Dinner: '🍽️',
  Snack: '🍎',
};

// One glyph per macro, used everywhere a macro is shown so the numbers are read by shape.
export const KCAL_EMOJI = '⚡';
export const PROTEIN_EMOJI = '🥩';
export const CARBS_EMOJI = '🍚';
export const FAT_EMOJI = '🧈';

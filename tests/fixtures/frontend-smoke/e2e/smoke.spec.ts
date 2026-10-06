import { test, expect } from '@playwright/test';

test('renders and reacts in Chromium', async ({ page }) => {
  const errors: string[] = [];
  page.on('pageerror', error => errors.push(error.message));
  await page.goto('/');
  await expect(page.getByRole('heading', { level: 1 })).toHaveText('Profile smoke');
  await page.getByRole('button', { name: 'Increment' }).click();
  await expect(page.getByLabel('Count')).toHaveText('1');
  expect(errors).toEqual([]);
});
import { TestBed } from '@angular/core/testing';
import { App } from './app';

describe('smoke application', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [App] }).compileComponents();
  });

  it('renders the neutral title', async () => {
    const fixture = TestBed.createComponent(App);
    await fixture.whenStable();
    expect(fixture.nativeElement.querySelector('h1').textContent).toBe('Profile smoke');
  });

  it('updates the count after an interaction', async () => {
    const fixture = TestBed.createComponent(App);
    await fixture.whenStable();
    fixture.nativeElement.querySelector('button').click();
    await fixture.whenStable();
    expect(fixture.nativeElement.querySelector('output').textContent).toBe('1');
  });
});
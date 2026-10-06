import { Component, signal } from '@angular/core';

@Component({
  selector: 'app-root',
  template: '<main><h1>Profile smoke</h1><button type="button" (click)="increment()">Increment</button><output aria-label="Count">{{ count() }}</output></main>',
})
export class App {
  protected readonly count = signal(0);

  increment(): void {
    this.count.update(value => value + 1);
  }
}
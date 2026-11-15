import { Directive, Injectable, OnDestroy, TemplateRef, inject, signal } from '@angular/core';

/** Holds the current page's main action (spec §6: the header holds the page's main action). */
@Injectable({ providedIn: 'root' })
export class HeaderActionSlot {
  readonly template = signal<TemplateRef<unknown> | null>(null);
}

/**
 * Put on an <ng-template> in a page to render it in the shell header, at the right, while the page
 * is shown. The template keeps the page's context, so its bindings and handlers are the page's.
 *
 *   <ng-template appHeaderAction>
 *     <button mat-flat-button class="header-action" (click)="create()">...</button>
 *   </ng-template>
 */
@Directive({ selector: 'ng-template[appHeaderAction]' })
export class HeaderAction implements OnDestroy {
  private readonly slot = inject(HeaderActionSlot);
  private readonly template = inject(TemplateRef);

  constructor() {
    this.slot.template.set(this.template);
  }

  ngOnDestroy(): void {
    if (this.slot.template() === this.template) {
      this.slot.template.set(null);
    }
  }
}

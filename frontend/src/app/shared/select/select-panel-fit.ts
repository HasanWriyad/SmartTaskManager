import { Directive, Injector, afterNextRender, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatSelect } from '@angular/material/select';

/**
 * Applies to every <mat-select> in a component that imports it: the options panel is at least as wide
 * as its field and grows to fit the longest option on one line. By default Material makes the panel
 * exactly the field's width, so short fields wrapped options like "In progress" onto two lines.
 *
 * When the panel opens, the overlay pane has the field width inline; after the panel has rendered
 * (the first moment its element can be found) that width becomes the pane's min-width. The overlay's
 * positioning box still limits the pane to the space left in the viewport, where long options end in
 * an ellipsis (global styles keep option text on one line). The panel's open animation hides the resize.
 */
@Directive({ selector: 'mat-select' })
export class SelectPanelFit {
  private readonly select = inject(MatSelect);
  private readonly injector = inject(Injector);

  constructor() {
    this.select.stateChanges.pipe(takeUntilDestroyed()).subscribe(() => {
      if (this.select.panelOpen) {
        afterNextRender(() => this.fit(), { injector: this.injector });
      }
    });
  }

  private fit(): void {
    const panel = this.select.panel?.nativeElement as HTMLElement | undefined;
    const pane = panel?.closest<HTMLElement>('.cdk-overlay-pane');
    if (pane?.style.width) {
      pane.style.minWidth = pane.style.width;
      pane.style.width = '';
    }
  }
}

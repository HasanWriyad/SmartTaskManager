import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import {
  AbstractControl,
  NonNullableFormBuilder,
  ReactiveFormsModule,
  ValidationErrors,
  ValidatorFn,
  Validators,
} from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { Router, RouterLink } from '@angular/router';
import { ApiError } from '../../../core/api/api-error';
import { AuthService } from '../../../core/auth/auth.service';
import { AuthLayout } from '../auth-layout/auth-layout';
import { applyServerErrors } from '../../../core/forms/server-errors';

/** Same rules as the backend's RegisterRequestValidator, so most mistakes are caught before submitting. */
const passwordRules: ValidatorFn[] = [
  Validators.required,
  Validators.minLength(8),
  Validators.maxLength(128),
  Validators.pattern(/[A-Z]/),
  Validators.pattern(/[a-z]/),
  Validators.pattern(/[0-9]/),
  Validators.pattern(/[^a-zA-Z0-9]/),
];

const passwordsMatch: ValidatorFn = (group: AbstractControl): ValidationErrors | null =>
  group.get('password')?.value === group.get('confirmPassword')?.value ? null : { passwordMismatch: true };

@Component({
  selector: 'app-register',
  imports: [
    ReactiveFormsModule,
    AuthLayout,
    RouterLink,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
  ],
  templateUrl: './register.html',
  styleUrl: '../auth-page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Register {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  protected readonly submitting = signal(false);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly hidePassword = signal(true);

  protected readonly form = inject(NonNullableFormBuilder).group(
    {
      fullName: ['', [Validators.required, Validators.minLength(2), Validators.maxLength(100)]],
      email: ['', [Validators.required, Validators.email, Validators.maxLength(256)]],
      password: ['', passwordRules],
      confirmPassword: ['', Validators.required],
    },
    { validators: passwordsMatch },
  );

  /** Shown once the confirmation has been entered and left, and differs from the password. */
  protected passwordMismatch(): boolean {
    const confirm = this.form.controls.confirmPassword;
    return this.form.hasError('passwordMismatch') && confirm.touched && confirm.value !== '';
  }

  /** Lists the password rules that are not met yet, for the hint under the field. */
  protected missingPasswordRules(): string[] {
    const value = this.form.controls.password.value;
    const rules: [boolean, string][] = [
      [value.length >= 8, 'at least 8 characters'],
      [/[A-Z]/.test(value), 'an uppercase letter'],
      [/[a-z]/.test(value), 'a lowercase letter'],
      [/[0-9]/.test(value), 'a digit'],
      [/[^a-zA-Z0-9]/.test(value), 'a symbol'],
    ];
    return rules.filter(([met]) => !met).map(([, label]) => label);
  }

  protected submit(): void {
    if (this.form.invalid || this.submitting()) {
      this.form.markAllAsTouched();
      return;
    }

    const { fullName, email, password } = this.form.getRawValue();
    this.submitting.set(true);
    this.errorMessage.set(null);
    this.auth.register({ fullName, email, password }).subscribe({
      next: () => void this.router.navigateByUrl('/dashboard'),
      error: (error: ApiError) => {
        this.submitting.set(false);
        if (error.status === 409) {
          this.form.controls.email.setErrors({ server: error.message });
          this.form.controls.email.markAsTouched();
        } else if (!applyServerErrors(this.form, error)) {
          this.errorMessage.set(error.message);
        }
      },
    });
  }
}

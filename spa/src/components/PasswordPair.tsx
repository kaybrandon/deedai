import PasswordField from "./PasswordField";
import { confirmPasswordError, validatePassword } from "../password";

export function passwordPairErrors(password: string, confirm: string, required: boolean) {
  const nextPassword = validatePassword(password, required);
  const nextConfirm = password || confirm ? confirmPasswordError(password, confirm) : required ? "Confirm the new password." : null;
  return { password: nextPassword, confirm: nextConfirm };
}

export default function PasswordPair({
  id,
  passwordLabel,
  confirmLabel = "Confirm New Password",
  password,
  confirm,
  passwordError,
  confirmError,
  required,
  onPassword,
  onConfirm
}: {
  id: string;
  passwordLabel: string;
  confirmLabel?: string;
  password: string;
  confirm: string;
  passwordError?: string | null;
  confirmError?: string | null;
  required?: boolean;
  onPassword: (value: string) => void;
  onConfirm: (value: string) => void;
}) {
  const confirmId = `${id}-confirm`;
  const errorId = `${confirmId}-error`;
  return (
    <>
      <PasswordField
        id={id}
        label={passwordLabel}
        value={password}
        required={required}
        error={passwordError}
        onChange={onPassword}
      />
      <label>
        {confirmLabel}
        <input
          id={confirmId}
          type="password"
          value={confirm}
          onChange={(e) => onConfirm(e.target.value)}
          required={required || Boolean(password)}
          autoComplete="new-password"
          aria-invalid={confirmError ? true : undefined}
          aria-describedby={confirmError ? errorId : undefined}
        />
        {confirmError && (
          <span className="field-error" id={errorId} role="alert">
            {confirmError}
          </span>
        )}
      </label>
    </>
  );
}

export const PASSWORD_HINT =
  "At least 8 characters, with uppercase, lowercase, a digit, and a symbol.";

export const PASSWORD_REQUIREMENT_MESSAGE =
  "Password must be at least 8 characters and include an uppercase letter, a lowercase letter, a digit, and a symbol.";

export function validatePassword(password: string, required: boolean): string | null {
  if (!password.trim()) {
    return required ? "Password is required." : null;
  }

  const chars = [...password];
  const hasUpper = chars.some((c) => /\p{Lu}/u.test(c));
  const hasLower = chars.some((c) => /\p{Ll}/u.test(c));
  const hasDigit = chars.some((c) => /\p{Nd}/u.test(c));
  const hasSymbol = chars.some((c) => !/\p{L}|\p{Nd}/u.test(c));

  if (password.length < 8 || !hasUpper || !hasLower || !hasDigit || !hasSymbol) {
    return PASSWORD_REQUIREMENT_MESSAGE;
  }

  return null;
}
